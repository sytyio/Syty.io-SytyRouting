using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;
using System.Collections.Concurrent;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public IEnumerable<Persona> Personas = new List<Persona>(0);
        public IEnumerable<Persona> PersonasWithRoute = new List<Persona>(0);

        private List<Persona> personas = new List<Persona>();
        private ConcurrentBag<Persona> personasWithRoute = new ConcurrentBag<Persona>();

        private Queue<Persona> personasQueue = new Queue<Persona>();


        private Graph _graph;

        private static int simultaneousTasks = (Environment.ProcessorCount > 1)? Environment.ProcessorCount: 1;
        private static int simultaneousRoutingTasks = simultaneousTasks; // // 1 additionanl task for fetching and dispatching data.

        private Task[] routingTasks = new Task[simultaneousRoutingTasks]; //


        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

    
        private int elementsToProcess = 0;
        private int processedDbElements = 0;
        private int computedRoutes = 0;

        private static int batchSize = simultaneousRoutingTasks * 100;
        private int routingTaskBatchSize = batchSize / simultaneousRoutingTasks;


        private int routingHasEnded = 0; // 0 == false

        private IRoutingAlgorithm[] routingAlgorithms = new IRoutingAlgorithm[simultaneousRoutingTasks];



        public PersonaRouter(Graph graph)
        {
            _graph = graph;
        }

        public IRoutingAlgorithm Init<T>() where T: IRoutingAlgorithm, new()
        {
            IRoutingAlgorithm routingAlgorithm = new T();
            return routingAlgorithm;
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            personas.Clear();
            personasWithRoute.Clear();

            for(var a = 0; a < routingAlgorithms.Length; a++)
            {
                routingAlgorithms[a] = new T();
                routingAlgorithms[a].Initialize(_graph);
            }

            logger.Info("Route searching using {0}'s algorithm running {1} (simultaneous) routing task(s)", routingAlgorithms[0].GetType().Name, simultaneousRoutingTasks);

            var connectionString = Constants.ConnectionString;
            var tableName = "public.persona";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            // elementsToProcess = await Helper.DbTableRowCount(connection, tableName, logger);
            elementsToProcess = 1379;// 5139; // 5137; // 5137; // = 5137; To Crash the DB load.

            await connection.CloseAsync();


            var regularBatchSize = (batchSize > elementsToProcess) ? elementsToProcess : batchSize;
            var numberOfBatches = (elementsToProcess / regularBatchSize > 0) ? elementsToProcess / regularBatchSize : 1;
            var lastBatchSize = elementsToProcess - regularBatchSize * (numberOfBatches - 1);
            int[] batchSizes = GetBatchPartition(regularBatchSize, lastBatchSize, numberOfBatches);


            // //////////////////////////////////////////////////////////////////////////////////// //
            // persona data load and dispatch in sequential batches, routing in parallel per batch  //
            // //////////////////////////////////////////////////////////////////////////////////// //

            // Initialize process
            int offset = 0;
            int currentBatchNumber = 0;
            int currentBatchSize = batchSizes[0];

            Task loadTask = Task.Run(() => DBPersonaLoadAsync(offset, currentBatchSize, currentBatchNumber));

            while(personaTaskArraysQueue.Count < simultaneousRoutingTasks)
                Thread.Sleep(1000);
            
            for(int t = 0; t < routingTasks.Length; t++)
            {
                int taskIndex = t;
                routingTasks[taskIndex] = Task.Run(() => CalculateRoutes(routingAlgorithms[taskIndex], taskIndex));
            }

            offset += currentBatchSize;
            for(var batchNumber = 1; batchNumber < numberOfBatches; batchNumber++)
            {
                currentBatchSize = batchSizes[batchNumber];

                while(personaTaskArraysQueue.Count > simultaneousRoutingTasks)
                    Thread.Sleep(1000);

                await DBPersonaLoadAsync(offset, currentBatchSize, batchNumber);

                offset += currentBatchSize;

                var timeSpan = stopWatch.Elapsed;
                var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, timeSpanMilliseconds, logger);
            }

            while(routingHasEnded != 1)
            {
                for(int t = 0; t < routingTasks.Length; t++ )
                    logger.Debug("Task #{0} (Id{1}) status: {2}", t, routingTasks[t].Id, routingTasks[t].Status);
                logger.Info("DB elements already processed: {0} of {1} in total. Computed routes: {2} ({3:0.000} %)", processedDbElements, elementsToProcess, computedRoutes, (double)computedRoutes / elementsToProcess * 100);
                logger.Info("Waiting for the routing process to complete...");

                var timeSpan = stopWatch.Elapsed;
                var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, timeSpanMilliseconds, logger);

                Thread.Sleep(5000);
            }

            Task.WaitAll(routingTasks);

            for(int t = 0; t < routingTasks.Length; t++ )
                logger.Debug("Task #{0} (Id{1}) status: {2}", t, routingTasks[t].Id, routingTasks[t].Status);

            Personas = personas.ToList();
            PersonasWithRoute = personasWithRoute.ToList();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time using {0} algorithm :: {1}", routingAlgorithms[0].GetType().Name, totalTime);
        }

        private async Task DBPersonaLoadAsync(int offset, int currentBatchSize, int batchNumber)
        {
            logger.Debug("> DBPersonaLoadAsync started on Thread #{0}", Thread.CurrentThread.ManagedThreadId);
            logger.Debug("Batch {0} :: Size: {1}, Offset: {2}", batchNumber, currentBatchSize, offset);

            var lastRoutingTaskBatchSize = currentBatchSize - routingTaskBatchSize * (simultaneousRoutingTasks - 1);
            int[] routingTaskBatchSizes = GetBatchPartition(routingTaskBatchSize, lastRoutingTaskBatchSize, simultaneousRoutingTasks);

            var connectionString = Constants.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                     0              1              2
            var queryString = "SELECT id, home_location, work_location FROM public.persona ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                    var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                    var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
                    personas.Add(persona);
                    personasQueue.Enqueue(persona);

                    processedDbElements++;
                }
            }

            for (var taskIndex = 0; taskIndex < simultaneousRoutingTasks; taskIndex++)
            {
                Persona[] personaTaskArray = new Persona[routingTaskBatchSizes[taskIndex]];

                for(var p = 0; p < routingTaskBatchSizes[taskIndex]; p++)
                {
                    if(personasQueue.TryDequeue(out Persona? persona))
                    {
                        personaTaskArray[p] = persona;
                    }
                    else
                    {
                        logger.Debug("Unable to retrieve persona from personaQueue");
                    }
                }
                personaTaskArraysQueue.Enqueue(personaTaskArray);
            }                

            await connection.CloseAsync();

            logger.Debug("Number of DB elements processed: {0} (of {1})", processedDbElements, elementsToProcess);
            if(processedDbElements > elementsToProcess)
            {
                logger.Debug("Inconsistent number of processed elements.");
            }
            logger.Debug("> DBPersonaLoadAsync ended on Thread #{0}", Thread.CurrentThread.ManagedThreadId);
        }

        private void CalculateRoutes(IRoutingAlgorithm routingAlgorithm, int taskIndex)
        {
            logger.Debug("> CalculateRoutes for routingTask {0} started on Thread #{1}", taskIndex, Thread.CurrentThread.ManagedThreadId);

            while(personaTaskArraysQueue.TryDequeue(out Persona[]? personaArray))
            {
                for(var i = 0; i < personaArray.Length; i++)
                {
                    var persona = personaArray[i];
                    var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                    var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                    var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);

                    personasWithRoute.Add(persona);

                    if(elementsToProcess == Interlocked.Increment(ref computedRoutes))
                    {
                        Interlocked.Exchange(ref routingHasEnded, 1);
                    }
                }
                logger.Debug("DB elements already processed: {0} of {1} in total. Computed routes: {2} ({3:0.000} %)", processedDbElements, elementsToProcess, computedRoutes, (double)computedRoutes / elementsToProcess * 100);
            }

            logger.Debug("RoutingTask {0}: Unable to retreive parsonaArray: Thread #{1}", taskIndex, Thread.CurrentThread.ManagedThreadId);
            logger.Debug("> CalculateRoutes for routingTask {0} ended on Thread #{1}", taskIndex, Thread.CurrentThread.ManagedThreadId);
        }

        public int[] GetBatchPartition(int regularSlice, int lastSlice, int partionElements)
        {
            int[] batchPartition = new int[partionElements];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        public void TracePersonas()
        {
            foreach (var persona in Personas)
            {
                logger.Trace("Persona: Id = {0},\n\t\t HomeLocation = ({1}, {2}),\n\t\t WorkLocation = ({3}, {4})",
                    persona.Id, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                    persona.WorkLocation?.X, persona.WorkLocation?.Y);
            }
        }

        public void TracePersonasIds()
        {
            logger.Debug("Personas Ids:");
            foreach (var persona in Personas)
            {
                logger.Debug("Persona: Id = {0}", persona.Id);
            }
        }

        public void TraceSortedPersonasIds()
        {
            var sortedPersonas = Personas.OrderBy(p => p.Id).ToArray();
            logger.Debug("Sorted Personas Ids:");
            foreach (var persona in sortedPersonas)
            {
                logger.Debug("Persona: Id = {0}", persona.Id);
            }
        }

        public void TracePersonasWithRouteIds()
        {
            logger.Debug("Personas with calculated route : Ids:");
            foreach (var persona in PersonasWithRoute)
            {
                logger.Debug("Persona: Id = {0}", persona.Id);
            }
        }

        public void TraceSortedPersonasWithRouteIds()
        {
            var sortedPersonasWithCalculatedRoute = PersonasWithRoute.OrderBy(p => p.Id).ToArray();
            logger.Debug("Sorted Personas with calculated route : Ids:");
            foreach (var persona in sortedPersonasWithCalculatedRoute)
            {
                logger.Debug("Persona: Id = {0}", persona.Id);
            }
        }

        public void TracePersonasWithCalculatedRoute()
        {
            var sortedPersonas = Personas.OrderBy(p => p.Id).ToArray();
            var sortedPersonasWithCalculatedRoute = PersonasWithRoute.OrderBy(p => p.Id).ToArray();

            var comparisonResult = Enumerable.SequenceEqual(sortedPersonas, sortedPersonasWithCalculatedRoute);

            if(comparisonResult)
            {
                logger.Info(" => Persona Ids sequences are equal.");
            }
            else
            {
                logger.Info(" => Persona Ids sequences are not equal.");
                var numberOfPersonas = sortedPersonas.Count();
                var numberOfPersonasWithCalculatedRoute = sortedPersonasWithCalculatedRoute.Count();

                var maxNumberOfItems = (numberOfPersonasWithCalculatedRoute >= numberOfPersonas)? numberOfPersonasWithCalculatedRoute : numberOfPersonas;
                
                var displayLimit = "";
                var itemsUpperLimit = 1000;
                if(maxNumberOfItems>itemsUpperLimit)
                {
                    maxNumberOfItems = itemsUpperLimit;
                    displayLimit = "(first " + maxNumberOfItems + " Ids)";
                }
                
                logger.Debug("{0,25} :: {1,25} {2}", "Sorted persona Ids", "Sorted persona Ids with calculated route", displayLimit);
                for(int i = 0; i < maxNumberOfItems; i++)
                {
                    string personaId  = "(Empty)";
                    if(i < numberOfPersonas)
                        personaId = sortedPersonas[i].Id.ToString();
                    string personaWithRouteId = "(Empty)";
                    if(i < numberOfPersonasWithCalculatedRoute)
                        personaWithRouteId = sortedPersonasWithCalculatedRoute[i].Id.ToString();
                    string nodeDifferenceMark = "";
                    if(!personaId.Equals(personaWithRouteId))
                        nodeDifferenceMark = "<<==";
                    logger.Debug("{0,25} :: {1,-25}\t\t{2}", personaId, personaWithRouteId, nodeDifferenceMark);
                }
            }
        }
    }
}
