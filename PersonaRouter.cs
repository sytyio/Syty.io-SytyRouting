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

        private static int simultaneousRoutingTasks = 1; // Environment.ProcessorCount;
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

        public async Task StartRouting<T>(byte[] transportModes) where T: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            // elementsToProcess = await Helper.DbTableRowCount(Configuration.PersonaTableName, logger);
            elementsToProcess = 20; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
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
                routingTasks[t] = Task.Run(() => CalculateRoutes<T>(t, transportModes));
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

        private void CalculateRoutes<T>(int taskIndex, byte[] requestedTransportModes) where T: IRoutingAlgorithm, new()
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
                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

                        // DEBUG
                        if(persona.Id == 2)
                        {
                            Console.WriteLine("Problemo");
                        }

                        persona.requestedTransportSequence = requestedTransportModes;                        
                        byte[] transportModesSequence = TransportModes.CreateTransportModeSequence(origin, destination, requestedTransportModes);
                        persona.definiteTransportSequence = transportModesSequence;
                        
                        if(TransportModes.ArrayToMask(transportModesSequence) !=0 && OriginAndDestinationAreValid(origin, destination, persona.Id))
                        {
                            var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID, transportModesSequence);
                            if(route.Count > 0)
                            {
                                TimeSpan currentTime = TimeSpan.Zero;
                                persona.Route = routingAlgorithm.ConvertRouteFromNodesToLineString(route, currentTime);
                                persona.TransportModeTransitions = routingAlgorithm.GetTransportModeTransitions();                                
                                persona.SuccessfulRouteComputation = true;

                                Interlocked.Increment(ref computedRoutes);
                            }
                            else
                            {
                                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
                            }
                        }
                        else
                        {
                            logger.Debug("Invalid transport sequence or invalid origin/destination data.");
                        }
                    }
                    catch (Exception e)
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                    }
                }
            }
        }

        private bool OriginAndDestinationAreValid(Node origin, Node destination, int personaId)
        {
            if(origin == null || destination == null)
            {
                logger.Debug(" ==>> Unable to compute route: Persona Id {0}: origin and/or destination nodes are invalid", personaId);
                return false;
            }
            else if(origin.Idx == destination.Idx)
            {
                logger.Debug("Origin and Destination Nodes are equal for Persona Id {0}", personaId);
                return false;
            }

            return true;
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
                    logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}", persona.Id);
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
            logger.Debug("Home location: ({0,18},{1,18})\t :: OSM Coordinates: {2,18},{3,18}\t : Origin OsmID      {4}", persona.HomeLocation!.X, persona.HomeLocation!.Y, persona.HomeLocation!.Y, persona.HomeLocation!.X, origin.OsmID);
            var originTransportModes = origin.GetAvailableOutboundTransportModes();
            logger.Debug("Avilable Outbound Transport modes for Node {0}: {1}", origin.OsmID, TransportModes.MaskToString(originTransportModes));
            logger.Debug("Work location: ({0,18},{1,18})\t :: OSM Coordinates: {2,18},{3,18}\t : Destination OsmID {4}", persona.WorkLocation!.X, persona.WorkLocation!.Y, persona.WorkLocation!.Y, persona.WorkLocation!.X, destination.OsmID);
            var destinationTransportModes = origin.GetAvailableInboundTransportModes();
            logger.Debug("Avilable Inbound Transport modes for Node {0}: {1}", destination.OsmID, TransportModes.MaskToString(originTransportModes));
        }

        public void TracePersonas()
        {
            logger.Debug("");
            logger.Debug("Personas:");
            foreach (var persona in personas)
            {
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                logger.Debug("");
                logger.Debug("Id {0}:\t HomeLocation = {1}:({2}, {3}),\t WorkLocation = {4}:({5}, {6})",
                    persona.Id, origin.OsmID, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                                destination.OsmID, persona.WorkLocation?.X, persona.WorkLocation?.Y);
                if(persona.requestedTransportSequence is not null)
                    logger.Debug("Requested Transport Mode sequence: {0}", TransportModes.NamesToString(TransportModes.ArrayToNames(persona.requestedTransportSequence)));
                if(persona.definiteTransportSequence is not null)
                    logger.Debug("Definite Transport Mode sequence: {0}", TransportModes.NamesToString(TransportModes.ArrayToNames(persona.definiteTransportSequence)));
                TraceRoute(persona);
            }
        }

        public void TraceRoute(Persona persona)
        {
            if(persona.Route is not null)
            {
                //TraceRoute(persona.Route);
                TraceRouteDetails(persona.Route, persona.TransportModeTransitions);
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

        public void TraceRouteDetails(LineString route, Dictionary<int, byte>? transportModeTransitions)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;


            // DEBUG
            if(transportModeTransitions!=null)
            {
                foreach(var transportModeTransition in transportModeTransitions)
                {
                    logger.Debug("Transport Mode transitions :: {0}:{1}: {2}", transportModeTransition.Key, transportModeTransition.Value, TransportModes.MaskToString(transportModeTransition.Value));
                }
            

                logger.Debug("> Route ({0} vertices)", routeCoordinates.Length);
                string vertexString        = String.Format(" {0,14}","Vertex");
                string nodeString          = String.Format(" {0,14}","Node OSM Id");
                string timeStampString     = String.Format(" {0,14}","Time stamp");
                string coordinateXString   = String.Format(" {0,14}","Coordinate X");
                string coordinateYString   = String.Format(" {0,14}","Coordinate Y");
                string transportModeString = String.Format(" {0,14}","Transport Mode");
                string nodeIdxString       = String.Format(" {0,14}","Node Idx");
                logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", vertexString, coordinateXString, coordinateYString, timeStampString, transportModeString, nodeIdxString, nodeString);
                logger.Debug("==================================================================================================================");
                
                int previousNodeIdx = 0;
                for(var n = 0; n < routeCoordinates.Length; n++)
                {
                    node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[n].M));
                    vertexString      = String.Format(" {0,14}", n+1);
                    nodeString        = String.Format(" {0,14}", node.OsmID);
                    timeStampString   = String.Format(" {0,14}", timeStamp);
                    coordinateXString = String.Format(" {0,14}", routeCoordinates[n].X);
                    coordinateYString = String.Format(" {0,14}", routeCoordinates[n].Y);
                    nodeIdxString     = String.Format(" {0,14}", node.Idx);
                    if(previousNodeIdx!=node.Idx && transportModeTransitions.ContainsKey(node.Idx))
                        transportModeString = String.Format(" {0,14}",TransportModes.MaskToString(transportModeTransitions[node.Idx]));
                    else
                        transportModeString = String.Format("{0,14}",".");
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", vertexString, coordinateXString, coordinateYString, timeStampString, transportModeString, nodeIdxString, nodeString);
                    if(n>10)
                    {
                        logger.Debug("{0,14}\t{1,14}\t{2,14}\t{3,14}\t{4,14}\t{4,14}",".",".",".",".",".",".");
                        logger.Debug("{0,14}\t{1,14}\t{2,14}\t{3,14}\t{4,14}\t{4,14}",".",".",".",".",".",".");
                        logger.Debug("{0,14}\t{1,14}\t{2,14}\t{3,14}\t{4,14}\t{4,14}",".",".",".",".",".",".");
                        break;
                    }
                    previousNodeIdx = node.Idx;
                }
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
                timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M));
                vertexString      = String.Format(" {0,14}", routeCoordinates.Length);
                nodeString          = String.Format(" {0,14}", node.OsmID);
                timeStampString     = String.Format(" {0,14}", timeStamp);
                coordinateXString   = String.Format(" {0,14}", routeCoordinates[route.Count -1].X);
                coordinateYString   = String.Format(" {0,14}", routeCoordinates[route.Count -1].Y);
                if(transportModeTransitions.ContainsKey(node.Idx))
                    transportModeString = String.Format(" {0,14}",TransportModes.MaskToString(transportModeTransitions[node.Idx]));
                else
                    transportModeString = String.Format("{0,14}",".");
                nodeIdxString       = String.Format(" {0,14}", node.Idx);
                logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", vertexString, coordinateXString, coordinateYString, timeStampString, transportModeString, nodeIdxString, nodeString);
            }
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
