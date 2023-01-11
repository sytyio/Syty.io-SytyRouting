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

        private static DateTime baseDateTime = DateTime.Parse("1970-01-01T00:00:00.0000000+01:00"); //Time Zone: Brussels +1

        public PersonaRouter(Graph graph)
        {
            _graph = graph;
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            // elementsToProcess = await Helper.DbTableRowCount(Configuration.PersonaTable, logger);
            //elementsToProcess = 6; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
            elementsToProcess = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTable, logger);

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
            //Task monitorTask = Task.Run(() => MonitorRouteCalculation());

            Task.WaitAll(routingTasks);
            routingTasksHaveEnded = true;
            //Task.WaitAll(monitorTask);

            //await DBPersonaRoutesUploadAsync();
            await DBRouteBenchmarkUploadAsync();

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

            //var personaTable = Configuration.PersonaTable;
            var personaTable = Configuration.RoutingBenchmarkTable;

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
                //                        0   1              2              3
                var queryString = "SELECT id, home_location, work_location, transport_sequence FROM " + personaTable + " ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)
                        var requestedSequence = reader.GetValue(3); // transport_sequence (text[])
                        string[] requestedTransportSequence;
                        if(requestedSequence is not null && requestedSequence != DBNull.Value)
                             requestedTransportSequence = (string[])requestedSequence;
                        else
                            requestedTransportSequence = new string[] {""};

                        var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation, RequestedTransportSequence = TransportModes.NamesToArray(requestedTransportSequence)};
                        
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
                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

                        if(origin!=destination)
                        {
                            var requestedTransportModes = persona.RequestedTransportSequence;

                            var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID, requestedTransportModes);

                            if(route.Count > 0)
                            {
                                TimeSpan currentTime = TimeSpan.Zero;

                                persona.Route = routingAlgorithm.NodeRouteToLineStringMMilliseconds(route, currentTime);

                                persona.TransportModeTransitions = routingAlgorithm.GetTransportModeTransitions();

                                persona.TTextTransitions = TransportTransitionsToTTEXTSequence(persona.Route, persona.TransportModeTransitions);

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
                            logger.Debug("Origin and destination nodes are equal for Persona Id {0}", persona.Id);
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

            var routeTable = Configuration.ComputedRouteTable;

            await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS " + routeTable + " (persona_id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY, route GEOMETRY);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            int uploadFails = 0;
            foreach(var persona in personas)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routeTable + " (persona_id, route) VALUES ($1, $2) ON CONFLICT (persona_id) DO UPDATE SET route = $2", connection)
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

        private async Task DBRouteBenchmarkUploadAsync()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;            
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var routeTable = Configuration.RoutingBenchmarkTable;

            int uploadFails = 0;
            foreach(var persona in personas)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routeTable + " (id, computed_route) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET computed_route = $2", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = persona.Route },
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();

                    if(persona.Route is not null)
                    {
                        var routeMSeconds = ConverRouteMMillisecondsToMSeconds(persona.Route);

                        await using var cmd_insert_tgeompoint = new NpgsqlCommand("INSERT INTO " + routeTable + " (id, computed_route_m_seconds) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET computed_route_m_seconds = $2", connection)
                        {
                            Parameters =
                            {
                                new() { Value = persona.Id },
                                new() { Value = routeMSeconds },
                            }
                        };
                    
                        await cmd_insert_tgeompoint.ExecuteNonQueryAsync();
                    }
                        
                    var transportModes = persona.TTextTransitions.Item1;
                    var timeStampsTZ = persona.TTextTransitions.Item2;
                    
                    await using var cmd_insert_ttext = new NpgsqlCommand("INSERT INTO " + routeTable + " (id, transport_modes, time_stamps) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET transport_modes = $2, time_stamps = $3", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = transportModes },
                            new() { Value = timeStampsTZ },
                        }
                    };
                
                    await cmd_insert_ttext.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}", persona.Id);
                    uploadFails++;
                }
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET computed_route_2d = st_force2d(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = st_IsValidTrajectory(computed_route_m_seconds);", connection))
            //await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = true;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route_m_seconds);", connection))
            //await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = true WHERE st_IsEmpty(computed_route_m_seconds);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET computed_route_temporal_point = computed_route_m_seconds::tgeompoint WHERE is_valid_route = true;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            //PLGSQL: Iterates over each transport mode transition to create the corresponding temporal text type sequence (ttext(Sequence)) for each valid route
            var iterationString = @"
            DO 
            $$
            DECLARE
            _id int;
            _arr_tm text[];
            _arr_ts timestamptz[];
            BEGIN    
                FOR _id, _arr_tm, _arr_ts in SELECT id, transport_modes, time_stamps FROM routing_benchmark ORDER BY id ASC
                LOOP
                    RAISE NOTICE 'id: %', _id;
                    RAISE NOTICE 'transport modes: %', _arr_tm;
                    RAISE NOTICE 'time stamps: %', _arr_ts;
                    UPDATE routing_benchmark SET transport_transitions = coalesce_transport_modes_time_stamps(_arr_tm, _arr_ts) WHERE is_valid_route = true AND id = _id;
                END LOOP;
            END;
            $$;
            ";

            

            await using (var cmd = new NpgsqlCommand(iterationString, connection))
            {
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch(Exception e)
                {
                    logger.Debug(" ==>> Unable to compute transport mode transitions on the database: {0}", e.Message);
                }
                
            }
   
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Routes successfully uploaded to the database in {1} (d.hh:mm:s.ms)", personas.Count - uploadFails,  totalTime);
            logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personas.Count, 100 * uploadFails / personas.Count);
        }

        // private LineString ConverRouteMMillisecondsToMSeconds(LineString route)
        // {
        //     var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
        //     var geometryFactory = new GeometryFactory(sequenceFactory);

        //     if(route.Count <= 1)
        //     {
        //         return new LineString(null, geometryFactory);
        //     }

        //     var newRoute = route.Copy();
        //     var newRouteCoordinates = newRoute.Coordinates;
        //     var mOrdinates = new TimeSpan[newRouteCoordinates.Length];

        //     for(var i = 0; i < newRouteCoordinates.Length; i++)
        //     {   
        //         mOrdinates[i] = TimeSpan.FromMilliseconds(newRouteCoordinates[i].M);
        //     }

        //     var coordinateSequence = new DotSpatialAffineCoordinateSequence(newRouteCoordinates, Ordinates.XYM);
        //     for(var i = 0; i < coordinateSequence.Count; i++)
        //     {
        //         coordinateSequence.SetM(i, mOrdinates[i].TotalSeconds);
        //     }
        //     coordinateSequence.ReleaseCoordinateArray();

        //     return new LineString(coordinateSequence, geometryFactory);
        // }

        private LineString ConverRouteMMillisecondsToMSeconds(LineString route)
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);

            if(route.Count <= 1)
            {
                return new LineString(null, geometryFactory);
            }

            var newRoute = route.Copy();
            var newRouteCoordinates = newRoute.Coordinates;
            var mOrdinates = new TimeSpan[newRouteCoordinates.Length];

            for(var i = 0; i < newRouteCoordinates.Length; i++)
            {   
                mOrdinates[i] = TimeSpan.FromMilliseconds(newRouteCoordinates[i].M);
            }

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(newRouteCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i].TotalMilliseconds / 1000.0);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, geometryFactory);
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
                
                TraceRoute(persona);
            }
        }

        public void TraceRoute(Persona persona)
        {
            if(persona.Route is not null && persona.TransportModeTransitions is not null)
            {
                // TraceRoute(persona.Route);
                TraceRouteDetails(persona.Route, persona.TransportModeTransitions);
                // TransportTransitionsToTTEXT(persona.Route, persona.TransportModeTransitions);
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

        public void TraceFullRoute(LineString route)
        {
            var routeCoordinates = route.Coordinates;
            
            logger.Debug("GeometryType:", route.GeometryType);
            logger.Debug("IsClosed: {0}", route.IsClosed);
            logger.Debug("IsEmpy: {0}", route.IsEmpty);
            logger.Debug("IsGeometryCollection: {0}", route.IsSimple);
            logger.Debug("IsValid: {0}", route.IsValid);

            Node node;
            string timeStamp;

            logger.Debug("> Route ({0})", routeCoordinates.Length);
            logger.Debug(String.Format(" Index :    --------- Coordinates ----------      ::                Node  ::                   M ::         Time stamp"));
            logger.Debug(String.Format("       :                  X ::                  Y ::                      ::                   M ::                   "));

            double previousM=0.0;
            double  currentM=0.0;
            for(var n = 0; n < routeCoordinates.Length; n++)
            {
                currentM = routeCoordinates[n].M;
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                if(route.Coordinates[n].M<double.MaxValue)
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[n].M));
                else
                    timeStamp = "Inf <<<===";
                if(previousM>=currentM)
                    timeStamp = " " + timeStamp + " <<<=== M ordinate inconsistency"; 
                logger.Debug(String.Format("{0,6} : {1,18} :: {2,18} :: {3,20} :: {4,20} :: {5,15}",n+1,routeCoordinates[n].X,routeCoordinates[n].Y, node.OsmID, routeCoordinates[n].M, timeStamp));
                previousM = currentM;
            }
        }

        public void TraceRouteDetails(LineString route, Dictionary<int, Tuple<byte,int>>? transportModeTransitions)
        {
            var routeCoordinates = route.Coordinates;

            Node node;
            string timeStamp;

            if(transportModeTransitions!=null)
            {
                try{
                    foreach(var transportModeTransition in transportModeTransitions)
                    {
                       logger.Debug("Transport Mode transitions :: {0}:{1}: {2}", transportModeTransition.Key, transportModeTransition.Value, TransportModes.MaskToString(transportModeTransition.Value.Item1));
                    }

                    logger.Debug("> Route ({0} vertices)", routeCoordinates.Length);
                    if(routeCoordinates.Length<1)
                    {
                        logger.Debug("> Empty route");
                        return;
                    }
                    string vertexS        = String.Format("{0,8}","Vertex");
                    string nodeS          = String.Format("{0,20}","Node OSM Id");
                    string timeStampS     = String.Format("{0,14}","Time stamp");
                    string coordinateXS   = String.Format("{0,20}","Coordinate X");
                    string coordinateYS   = String.Format("{0,20}","Coordinate Y");
                    string transportModeS = String.Format("{0,18}","Transport Mode");
                    string routeTypeS     = String.Format("{0,10}","Route Type");
                    string routeTagS      = String.Format("{0,30}","Route Tag (Value : Key)");
                    string routeTransportModesS      = String.Format(" {0,30}","Route Allowed Transport Modes");
                    string nodeIdxS       = String.Format(" {0,14}","Node Idx");
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                    logger.Debug("=======================================================================================================================================================================================================================");
                    
                    int transportModeRepetitions=0;
                    byte currentTransportMode = 0;
                    byte previousTransportMode = 0;
                    for(var n = 0; n < routeCoordinates.Length-1; n++)
                    {
                        node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);

                        if(transportModeTransitions.ContainsKey(node.Idx))
                        {
                            currentTransportMode = transportModeTransitions[node.Idx].Item1;
                        }

                        if(previousTransportMode!=currentTransportMode)
                        {
                            previousTransportMode = currentTransportMode;    
                            transportModeS = String.Format("{0,18}",TransportModes.MaskToString(currentTransportMode));
                            var routeType = transportModeTransitions[node.Idx].Item2;
                            routeTypeS     = String.Format("{0,10}",routeType);
                            if(TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                                routeTagS      = String.Format("{0,30}",TransportModes.OSMTagIdToKeyValue[routeType]);
                            else
                                routeTagS      = String.Format("{0,30}","Not available");
                            routeTransportModesS = String.Format("{0,30}",TransportModes.MaskToString(TransportModes.TagIdToTransportModes(routeType)));
                            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[n].M));
                            vertexS      = String.Format("{0,8}", n+1);
                            nodeS        = String.Format("{0,20}", node.OsmID);
                            timeStampS   = String.Format("{0,14}", timeStamp);
                            coordinateXS = String.Format("{0,20}", routeCoordinates[n].X);
                            coordinateYS = String.Format("{0,20}", routeCoordinates[n].Y);
                            nodeIdxS     = String.Format("{0,14}", node.Idx);
                            logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                            transportModeRepetitions=0;
                        }
                        else
                        {
                            if(transportModeRepetitions<1)
                                logger.Debug("{0,10}\t{1,20}\t{2,20}\t{3,14}\t{4,18}\t{5,10}\t{6,30}\t{7,30}\t{8,14}\t{9,20}","|  ","|","|","|","| ","|","|","| ","|","|");
                            transportModeRepetitions++;
                        }
                    }
                    node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M));
                    vertexS        = String.Format("{0,8}", routeCoordinates.Length);
                    nodeS          = String.Format("{0,20}", node.OsmID);
                    timeStampS     = String.Format("{0,14}", timeStamp);
                    coordinateXS   = String.Format("{0,20}", routeCoordinates[route.Count -1].X);
                    coordinateYS   = String.Format("{0,20}", routeCoordinates[route.Count -1].Y);
                    if(transportModeTransitions.ContainsKey(node.Idx))
                    {
                        transportModeS = String.Format("{0,18}",TransportModes.MaskToString(transportModeTransitions[node.Idx].Item1));
                        var routeType = transportModeTransitions[node.Idx].Item2;
                        routeTypeS     = String.Format("{0,10}",routeType);
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                            routeTagS      = String.Format("{0,30}",TransportModes.OSMTagIdToKeyValue[routeType]);
                        else
                            routeTagS      = String.Format("{0,30}","Not available");
                        routeTransportModesS = String.Format("{0,30}",TransportModes.MaskToString(TransportModes.TagIdToTransportModes(routeType)));
                    }
                    else
                    {
                        transportModeS = String.Format("{0,18}","| ");
                        routeTypeS     = String.Format("{0,10}","|");
                        routeTagS      = String.Format("{0,30}","|");
                        routeTransportModesS     = String.Format("{0,30}","| ");
                    }
                    nodeIdxS       = String.Format("{0,14}", node.Idx);
                    logger.Debug("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", vertexS, coordinateXS, coordinateYS, timeStampS, transportModeS, routeTypeS, routeTagS, routeTransportModesS, nodeIdxS, nodeS);
                }
                catch (Exception e)
                {
                    logger.Debug("Unable to display data:", e.Message);
                }
            }
        }

        private Tuple<string[],DateTime[]> TransportTransitionsToTTEXTSequence(LineString route, Dictionary<int,Tuple<byte,int>> transitions)
        {
            var coordinates = route.Coordinates;
            Node node;
            if(transitions == null || transitions.Count <1 || route.IsEmpty)
                return new Tuple<string[],DateTime[]>(new string[0], new DateTime[0]);
            
            List<DateTime> timeStamps = new List<DateTime>(transitions.Count);
            List<string> transportModes = new List<string>(transitions.Count);

            string ttextS = "";

            foreach(var transition in transitions)
            {
                //logger.Debug("Transport Mode transitions :: {0}:{1}: {2}", transition.Key, transition.Value, TransportModes.MaskToString(transition.Value.Item1));
            }
        
            string timeStampS     = String.Format("{0,14}","Time stamp");
            string transportModeS = String.Format("{0,18}","Transport Mode");
            
            //logger.Debug("{0}\t{1}", timeStampS, transportModeS);
            //logger.Debug("=======================================");
            
            int transportModeRepetitions=0;
            byte currentTransportMode = 0;
            byte previousTransportMode = 0;
            for(var n = 0; n < coordinates.Length-1; n++)
            {
                node = _graph.GetNodeByLongitudeLatitude(coordinates[n].X, coordinates[n].Y);

                if(transitions.ContainsKey(node.Idx))
                {
                    currentTransportMode = transitions[node.Idx].Item1;
                }

                if(previousTransportMode!=currentTransportMode)
                {
                    previousTransportMode = currentTransportMode;    
                    transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                    var routeType = transitions[node.Idx].Item2;
                    if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                        transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
                    

                    timeStamps.Add(baseDateTime.Add(TimeSpan.FromMilliseconds(route.Coordinates[n].M)));
                    transportModes.Add(transportModeS);


                    timeStampS   = String.Format("{0,14}", Helper.FormatElapsedTimeHHMMSS(TimeSpan.FromMilliseconds(route.Coordinates[n].M)));
                    
                    //logger.Debug("{0,14}\t{1,18}", timeStampS, transportModeS);
                    transportModeRepetitions=0;

                    //ttextS += "\"" + transportModeS + "\"" + "@1970-01-01 " + timeStamp + "+00,";
                }
                else
                {
                    if(transportModeRepetitions<1)
                        //logger.Debug("{0,14}\t{1,18}",":",":");
                    transportModeRepetitions++;
                } 
            }
            node = _graph.GetNodeByLongitudeLatitude(coordinates[route.Count -1].X, coordinates[route.Count -1].Y);
            //timeStamp = Helper.FormatElapsedTimeHHMMSS(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M));


            timeStamps.Add(baseDateTime.Add(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M)));


            timeStampS     = String.Format("{0,14}", Helper.FormatElapsedTimeHHMMSS(TimeSpan.FromMilliseconds(route.Coordinates[route.Count -1].M)));
            if(transitions.ContainsKey(node.Idx))
            {
                var routeType = transitions[node.Idx].Item2;
                if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
            }
            transportModes.Add(transportModeS);

            //ttextS += "\"" + transportModeS + "\"" + "@1970-01-01 " + timeStamp + "+00";
            //ttextS += "'" + "1970-01-01 " + timeStamp + "+00'::TIMESTAMPTZ";
            //ttextS += "]";

            //logger.Debug("{0,14}\t{1,18}", timeStampS, transportModeS);
            //logger.Debug("ttext string: {0}", ttextS);

            // return ttextS;
            return new Tuple<string[],DateTime[]>(transportModes.ToArray(), timeStamps.ToArray());
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
