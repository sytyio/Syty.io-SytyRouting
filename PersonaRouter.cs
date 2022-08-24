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

        private const string TableName = "public.persona";
        private const int MonitorSleepMilliseconds = 5_000;
        private const int DBPersonaLoadAsyncSleepMilliseconds = 100;
        private const int InitialDataLoadSleepMilliseconds = 2_000;

        private const int RegularRoutingTaskBatchSize = 100;


        public IEnumerable<Persona> Personas = new List<Persona>(0);
        private List<Persona> personas = new List<Persona>();
        private Queue<Persona> personasQueue = new Queue<Persona>();

        private Graph _graph;

        private static int simultaneousRoutingTasks = Environment.ProcessorCount;
        private Task[] routingTasks = new Task[simultaneousRoutingTasks];

        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

        private int taskArraysQueueThreshold = simultaneousRoutingTasks;

        private int elementsToProcess = 0;
        private int processedDbElements = 0;
        private static int computedRoutes = 0;
        private bool routingTasksHaveEnded = false;
    
        private int regularBatchSize = simultaneousRoutingTasks * RegularRoutingTaskBatchSize;


        Stopwatch stopWatch = new Stopwatch();


        public PersonaRouter(Graph graph)
        {
            _graph = graph;
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

            // elementsToProcess = await Helper.DbTableRowCount(TableName, logger);
            elementsToProcess = 1_000_000; // 13579;
            if(elementsToProcess < 1)
            {
                logger.Info("No DB elements to process");
                return;
            }
            
            Task loadingTask = Task.Run(() => DBPersonaLoadAsync());
            Thread.Sleep(InitialDataLoadSleepMilliseconds);

            if(personaTaskArraysQueue.Count < simultaneousRoutingTasks)
            {
                logger.Info(" ==>> Initial DB load timeout ({0} ms) elapsed. Unable to start the routing process.", InitialDataLoadSleepMilliseconds);
                return;
            }
            
            for(int taskIndex = 0; taskIndex < routingTasks.Length; taskIndex++)
            {
                int t = taskIndex;
                routingTasks[t] = Task.Run(() => CalculateRoutes<T>(t));
            }
            Task monitorTask = Task.Run(() => MonitorRouteCalculation());

            Task.WaitAll(routingTasks);
            routingTasksHaveEnded = true;
            Task.WaitAll(monitorTask);

            Personas = personas.ToList();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time :: {0}", totalTime);
        }

        private async Task DBPersonaLoadAsync()
        {
            var connectionString = Constants.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            var batchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;
            var numberOfBatches = (elementsToProcess / batchSize > 0) ? elementsToProcess / batchSize : 1;
            var lastBatchSize = elementsToProcess - batchSize * (numberOfBatches - 1);
            int[] batchSizes = GetBatchPartition(batchSize, lastBatchSize, numberOfBatches);

            int offset = 0;
            for(var batchNumber = 0; batchNumber < numberOfBatches; batchNumber++)
            {
                var currentBatchSize = batchSizes[batchNumber];

                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                     0              1              2
                var queryString = "SELECT id, home_location, work_location FROM " + TableName + " ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                        var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation};
                        personas.Add(persona);
                        personasQueue.Enqueue(persona);

                        processedDbElements++;
                    }
                }
                DispatchData(currentBatchSize);
                offset += currentBatchSize;

                while(personaTaskArraysQueue.Count > taskArraysQueueThreshold)
                    Thread.Sleep(DBPersonaLoadAsyncSleepMilliseconds);
            }
            await connection.CloseAsync();
        }

        private void DispatchData(int currentBatchSize)
        {
            var routingTaskBatchSize = currentBatchSize / simultaneousRoutingTasks;
            var lastRoutingTaskBatchSize = currentBatchSize - routingTaskBatchSize * (simultaneousRoutingTasks - 1);
            int[] routingTaskBatchSizes = GetBatchPartition(routingTaskBatchSize, lastRoutingTaskBatchSize, simultaneousRoutingTasks);
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
                        logger.Debug(" ==>> Unable to retrieve persona from personaQueue");
                    }
                }
                personaTaskArraysQueue.Enqueue(personaTaskArray);
            }                
        }

        private void CalculateRoutes<T>(int taskIndex) where T: IRoutingAlgorithm, new()
        {
            var routingAlgorithm = new T();
            routingAlgorithm.Initialize(_graph);
            
            while(personaTaskArraysQueue.TryDequeue(out Persona[]? personaArray))
            {
                for(var i = 0; i < personaArray.Length; i++)
                {
                    var persona = personaArray[i];
                    try
                    {
                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                        var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);
                        persona.Route = route.ToList();
                        persona.SuccessfulRouteComputation = true;

                        Interlocked.Increment(ref computedRoutes);
                    }
                    catch
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Info(" ==>> Unable to compute route: Persona Id {0}", persona.Id);
                    }
                }
            }
        }

        private void MonitorRouteCalculation()
        {
            while(true)
            {
                var timeSpan = stopWatch.Elapsed;
                var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, timeSpanMilliseconds, logger);
                logger.Info("DB elements already processed: {0} ({1:0.000} %). Computed routes: {2} ({3:0.000} %)", processedDbElements, (double)processedDbElements / elementsToProcess * 100, computedRoutes, (double)computedRoutes / elementsToProcess * 100);
                logger.Info("");

                if(routingTasksHaveEnded)
                {
                    if(processedDbElements != elementsToProcess)
                    {
                        logger.Info(" ==>> Inconsistent number of processed elements.");
                    }
                    return;
                }

                Thread.Sleep(MonitorSleepMilliseconds);
            }
        }

        public int[] GetBatchPartition(int regularSlice, int lastSlice, int numberOfSlices)
        {
            int[] batchPartition = new int[numberOfSlices];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        public void TracePersonas()
        {
            logger.Debug("Personas:");
            foreach (var persona in Personas)
            {
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                logger.Debug("Id {0}:\t HomeLocation = {1}:({2}, {3}),\t WorkLocation = {4}:({5}, {6})",
                    persona.Id, origin.OsmID, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                                destination.OsmID, persona.WorkLocation?.X, persona.WorkLocation?.Y);
                TracePersonaRoute(persona);
            }
        }

        public void TracePersonaRoute(Persona persona)
        {
            if(persona.Route is not null)
            {
                string routeString = String.Format(" > Route ({0}):", persona.Route.Count());
                for(var n = 0; n < persona.Route.Count; n++)
                {
                    routeString += String.Format(" {0},", persona.Route[n].OsmID);
                    if(n>2)
                        break;
                }
                routeString += String.Format(" ..., {0} ", persona.Route[persona.Route.Count -1].OsmID);
                logger.Debug(routeString);
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
    }
}
