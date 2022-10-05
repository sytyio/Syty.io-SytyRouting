using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;
using System.Collections.Concurrent;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private List<Persona> personas = new List<Persona>();
        
        private Graph _graph;

        private static int simultaneousRoutingTasks = Environment.ProcessorCount;
        private Task[] routingTasks = new Task[simultaneousRoutingTasks];

        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

        private int taskArraysQueueThreshold = simultaneousRoutingTasks;

        private int elementsToProcess = 0;
        private int processedDbElements = 0;
        private static int computedRoutes = 0;
        private bool routingTasksHaveEnded = false;
    
        private int regularBatchSize = simultaneousRoutingTasks * Configuration.RegularRoutingTaskBatchSize;


        private Stopwatch stopWatch = new Stopwatch();


        public PersonaRouter(Graph graph)
        {
            _graph = graph;
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            // elementsToProcess = await Helper.DbTableRowCount(Configuration.PersonaTableName, logger);
            elementsToProcess = 100; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
            if(elementsToProcess < 1)
            {
                logger.Info("No DB elements to process");
                return;
            }
            else if(elementsToProcess < simultaneousRoutingTasks)
            {
                simultaneousRoutingTasks = elementsToProcess;
            }
            
            Task loadingTask = Task.Run(() => DBPersonaDownloadAsync());
            Thread.Sleep(initialDataLoadSleepMilliseconds);
            if(personaTaskArraysQueue.Count < simultaneousRoutingTasks)
            {
                logger.Info(" ==>> Initial DB load timeout ({0} ms) elapsed. Unable to start the routing process.", initialDataLoadSleepMilliseconds);
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

            await DBPersonaRoutesUploadAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time :: {0}", totalTime);
        }

        private async Task DBPersonaDownloadAsync()
        {
            int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var personaTableName = Configuration.PersonaTableName;

            var batchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;
            var numberOfBatches = (elementsToProcess / batchSize > 0) ? elementsToProcess / batchSize : 1;
            int[] batchSizes = GetBatchPartition(batchSize, elementsToProcess, numberOfBatches);

            int offset = 0;
            for(var batchNumber = 0; batchNumber < numberOfBatches; batchNumber++)
            {
                var currentBatchSize = batchSizes[batchNumber];

                var routingTaskBatchSize = (currentBatchSize / simultaneousRoutingTasks > 0) ? currentBatchSize / simultaneousRoutingTasks : 1;
                int[] routingTaskBatchSizes = GetBatchPartition(routingTaskBatchSize, currentBatchSize, simultaneousRoutingTasks);

                var taskIndex = 0;
                var personaTaskArray = new Persona[routingTaskBatchSizes[taskIndex]];
                var personaIndex = 0;

                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                     0              1              2
                var queryString = "SELECT id, home_location, work_location FROM " + personaTableName + " ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

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
                        
                        personaTaskArray[personaIndex] = persona;
                        personaIndex++;

                        if(personaIndex >= routingTaskBatchSizes[taskIndex])
                        {
                            personaTaskArraysQueue.Enqueue(personaTaskArray);
                            personaIndex = 0;
                            taskIndex++;
                            if(taskIndex < simultaneousRoutingTasks)
                                personaTaskArray = new Persona[routingTaskBatchSizes[taskIndex]];
                        }
                        processedDbElements++;
                    }
                }
                offset += currentBatchSize;

                while(personaTaskArraysQueue.Count > taskArraysQueueThreshold)
                    Thread.Sleep(dBPersonaLoadAsyncSleepMilliseconds);
            }
            await connection.CloseAsync();
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
                        if(origin.Idx == destination.Idx)
                        {
                            logger.Debug("Origin and Destination Nodes are equal");
                        }
                        var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);
                        
                        TimeSpan currentTime = TimeSpan.Zero;
                        persona.Route = routingAlgorithm.ConvertRouteFromNodesToLineString(route, currentTime);
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
            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
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

                Thread.Sleep(monitorSleepMilliseconds);
            }
        }

        private int[] GetBatchPartition(int regularSlice, int whole, int numberOfSlices)
        {
            int lastSlice = whole - regularSlice * (numberOfSlices - 1);
            int[] batchPartition = new int[numberOfSlices];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        private async Task DBPersonaRoutesUploadAsync()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;            
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var routeTableName = Configuration.ComputedRouteTableName;

            await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS " + routeTableName + " (persona_id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY, route GEOMETRY);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            int uploadFails = 0;
            foreach(var persona in personas)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routeTableName + " (persona_id, route) VALUES ($1, $2) ON CONFLICT (persona_id) DO UPDATE SET route = $2", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = persona.Route },
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Info("Unable to upload route to database");
                    logger.Debug("> Persona Id{0}");
                    uploadFails++;
                }
            }
   
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Routes successfully uploaded to the database in {1} (d.hh:mm:s.ms)", personas.Count - uploadFails,  totalTime);
            logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personas.Count, 100 * uploadFails / personas.Count);
        }

        public void TracePersonaDetails(Persona persona)
        {
            var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
            var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
            logger.Debug("Persona details:");
            logger.Debug("Id {0}", persona.Id);
            logger.Debug("Home location: ({0,18},{1,18})\t : Origin OsmID      {2}", persona.HomeLocation!.X, persona.HomeLocation!.Y, origin.OsmID);
            logger.Debug("Work location: ({0,18},{1,18})\t : Destination OsmID {2}", persona.WorkLocation!.X, persona.WorkLocation!.Y, origin.OsmID);
        }

        public void TracePersonas()
        {
            logger.Debug("Personas:");
            foreach (var persona in personas)
            {
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                logger.Debug("Id {0}:\t HomeLocation = {1}:({2}, {3}),\t WorkLocation = {4}:({5}, {6})",
                    persona.Id, origin.OsmID, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                                destination.OsmID, persona.WorkLocation?.X, persona.WorkLocation?.Y);
                TraceRoute(persona);
            }
        }

        public void TraceRoute(Persona persona)
        {
            if(persona.Route is not null)
            {
                TraceRoute(persona.Route);
            }
        }

        public void TraceRoute(LineString route)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;

            logger.Debug("> Route ({0})", routeCoordinates.Length);
            string routeNodeString      = String.Format(">            Nodes:      ");
            string routeTimeStampString = String.Format(">            Time stamps:");
            for(var n = 0; n < routeCoordinates.Length; n++)
            {
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[n].M));
                routeNodeString += String.Format(" {0,14},", node.OsmID);
                routeTimeStampString += String.Format(" {0,14},", timeStamp);
                if(n>2)
                {
                    break;
                }  
            }
            node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M));
            routeNodeString += String.Format(" ..., {0,14} ", node.OsmID);
            routeTimeStampString += String.Format(" ..., {0,14} ", timeStamp);
            logger.Debug(routeNodeString);
            logger.Debug(routeTimeStampString);
        }

        public void TracePersonasRouteResult()
        {
            int routeComputationFails = 0;
            foreach (var persona in personas)
            {
                if(persona.SuccessfulRouteComputation is not true)
                {
                    logger.Debug("Persona: Id = {0}, route found = {1}", persona.Id, persona.SuccessfulRouteComputation);
                    TracePersonaDetails(persona);
                    routeComputationFails++;
                }
            }
            logger.Info("{0} routes missing", routeComputationFails);
        }
    }
}
