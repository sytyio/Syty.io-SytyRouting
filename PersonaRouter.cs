using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public IEnumerable<Persona> Personas = new List<Persona>(0);
        public IEnumerable<Persona> PersonasWithRoute = new List<Persona>(0);

        private Persona[] personas = new Persona[0];
        private Persona[] personasWithRoute = new Persona[0];

        private Graph _graph;

        private static int simultaneousTasks = (Environment.ProcessorCount > 1)? Environment.ProcessorCount: 1;
        private static int simultaneousRoutingTasks = simultaneousTasks; //
        private Task[] routingTasks = new Task[simultaneousRoutingTasks]; //
    
        private int elementsToProcess = 0;
        private int batchSize = simultaneousRoutingTasks * 100;

        private IRoutingAlgorithm[] routingAlgorithms = new IRoutingAlgorithm[simultaneousRoutingTasks];

        public PersonaRouter(Graph graph)
        {
            _graph = graph;

            for(var t = 0; t < routingTasks.Length; t++)
            {
                routingTasks[t] = Task.CompletedTask;
            }
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

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

            elementsToProcess = await Helper.DbTableRowCount(connection, tableName, logger);
            // elementsToProcess = 1111;

            await connection.CloseAsync();

            Array.Resize(ref personas, elementsToProcess);
            Array.Resize(ref personasWithRoute, elementsToProcess);   

            // //////////////////////////////////////////////////////////////////////////////////// //
            // persona data load and dispatch in sequential batches, routing in parallel per batch  //
            // //////////////////////////////////////////////////////////////////////////////////// //
            await DBPersonaLoadAsync();

            Task.WaitAll(routingTasks);

            Personas = personas.ToList();
            PersonasWithRoute = personasWithRoute.ToList();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time using {0} algorithm :: {1}",routingAlgorithms[0].GetType().Name, totalTime);
        }

        private async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            var regularBatchSize = (batchSize > elementsToProcess) ? elementsToProcess : batchSize;
            var numberOfBatches = (elementsToProcess / regularBatchSize > 0) ? elementsToProcess / regularBatchSize : 1;
            var lastBatchSize = elementsToProcess - regularBatchSize * (numberOfBatches - 1);
            int[] batchSizes = new int[numberOfBatches];
            for (var i = 0; i < batchSizes.Length-1; i++)
            {
                batchSizes[i] = regularBatchSize;
            }
            batchSizes[batchSizes.Length-1] = lastBatchSize;

            int personasIdx = 0;
            var offset = 0;
            for(var batchNumber = 0; batchNumber < numberOfBatches; batchNumber++)
            {
                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                     0              1              2
                var queryString = "SELECT id, home_location, work_location FROM public.persona ORDER BY id ASC LIMIT " + batchSizes[batchNumber] + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                        var persona = new Persona { Idx = personasIdx, Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
                        personas[personasIdx] = persona;

                        personasIdx++;
                    }
                }
                var timeSpan = stopWatch.Elapsed;
                var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                var t = Task.Run(() => Helper.DataLoadBenchmark(elementsToProcess, personasIdx, timeSpan, timeSpanMilliseconds, logger));

                DispatchData(batchSizes[batchNumber], personasIdx);
                offset = offset + batchSizes[batchNumber];
            }

            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);

            logger.Info("                       Persona set creation time :: " + totalTime);
            logger.Debug("Number of DB elements processed: {0} (of {1})", personasIdx, elementsToProcess);
        }

        private void DispatchData(int batchSize, int endReference)
        {
            var personasIndex = endReference - batchSize;

            var regularRoutingTaskBatchSize = batchSize / simultaneousRoutingTasks;
            var lastRoutingTaskBatchSize = batchSize - regularRoutingTaskBatchSize * (simultaneousRoutingTasks - 1);
            int[] routingTaskBatchSizes = new int[simultaneousRoutingTasks];
            for (var s = 0; s < routingTaskBatchSizes.Length-1; s++)
            {
                routingTaskBatchSizes[s] = regularRoutingTaskBatchSize;
            }
            routingTaskBatchSizes[routingTaskBatchSizes.Length-1] = lastRoutingTaskBatchSize;

            for (var t = 0; t < simultaneousRoutingTasks; t++)
            {
                Persona[] personaTaskArray = new Persona[routingTaskBatchSizes[t]];
                for(var p = 0; p < routingTaskBatchSizes[t]; p++)
                {
                    personaTaskArray[p] = personas[personasIndex];
                    personasIndex++;
                }
                ScheduleRoutingTask(personaTaskArray);
            }
        }

        private void ScheduleRoutingTask(Persona[] personaTaskArray)
        {
            for(var t = 0; t < routingTasks.Length; t++)
            {
                var taskIndex = t;
                if(routingTasks[taskIndex].IsCompleted)
                {
                    routingTasks[taskIndex] = Task.Run(() => CalculateRoutes(routingAlgorithms[taskIndex], personaTaskArray));
                    break;
                }
            }
            Task.WaitAny(routingTasks);
        }

        private void CalculateRoutes(IRoutingAlgorithm routingAlgorithm, Persona[] personaTaskArray)
        {
            for(var i = 0; i < personaTaskArray.Length; i++)
            {
                var persona = personaTaskArray[i];
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);

                personasWithRoute[persona.Idx]=persona;
            }
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
                if(maxNumberOfItems>100)
                {
                    maxNumberOfItems = 100;
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
