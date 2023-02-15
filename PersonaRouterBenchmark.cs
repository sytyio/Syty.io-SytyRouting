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
    public class PersonaRouterBenchmark
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private List<Persona> personas = new List<Persona>();
        
        private Graph _graph;

        private static int simultaneousRoutingTasks = 1;//Environment.ProcessorCount;

        private Task[] routingTasks = new Task[simultaneousRoutingTasks];

        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

        private int taskArraysQueueThreshold = simultaneousRoutingTasks;

        private int elementsToProcess = 0;
        private int processedDbElements = 0;
        private static int computedRoutes = 0;
        private bool routingTasksHaveEnded = false;
    
        private int regularBatchSize = simultaneousRoutingTasks * Configuration.RegularRoutingTaskBatchSize;

        private Stopwatch stopWatch = new Stopwatch();

        private int sequenceValidationErrors = 0;
        private int originEqualsDestinationErrors = 0;

        public PersonaRouterBenchmark(Graph graph)
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
            //debug: Task monitorTask = Task.Run(() => MonitorRouteCalculation());

            Task.WaitAll(routingTasks);
            routingTasksHaveEnded = true;
            //debug: Task.WaitAll(monitorTask);

            ////await DBPersonaRoutesUploadAsync();
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
                        {
                             requestedTransportSequence = ValidateTransportSequence(id, homeLocation, workLocation, (string[])requestedSequence);
                        }
                        else
                        {
                            requestedTransportSequence = new string[] {""};
                        }

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

        private string[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence)
        {
            byte initialTransportMode = TransportModes.NameToMask(transportSequence.First());
            byte finalTransportMode = TransportModes.NameToMask(transportSequence.Last());

            var originNode = _graph.GetNodeByLongitudeLatitude(homeLocation.X, homeLocation.Y, isSource: true);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(workLocation.X, workLocation.Y, isTarget: true);

            Edge outboundEdge = originNode.GetFirstOutboundEdge(initialTransportMode);
            Edge inboundEdge = destinationNode.GetFirstInboundEdge(finalTransportMode);

            if(outboundEdge != null && inboundEdge != null)
            {
                return transportSequence;
            }
            else
            {
                sequenceValidationErrors++;

                logger.Debug("!===================================!");
                logger.Debug(" Transport sequence validation error:");
                logger.Debug("!===================================!");
                logger.Debug(" Persona Id: {0}", id);
                if(outboundEdge == null)
                {
                    logger.Debug(" ORIGIN Node Idx {0} does not contain the requested transport mode '{1}'.",originNode.Idx,TransportModes.SingleMaskToString(initialTransportMode)); // Use MaskToString() if the first byte in the sequence represents more than one Transport Mode

                    var outboundEdgeTypes = originNode.GetOutboundEdgeTypes();
                    string outboundEdgeTypesS = "";
                    foreach(var edgeType in outboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            outboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Outbound Edge type(s): {0}",outboundEdgeTypesS);
                    
                    var outboundTransportModes = originNode.GetAvailableOutboundTransportModes();
                    logger.Debug(" Available outbound transport modes at origin: {0}",TransportModes.MaskToString(outboundTransportModes));
                    initialTransportMode = TransportModes.MaskToArray(outboundTransportModes).First();
                }

                if(inboundEdge == null)
                {
                    logger.Debug(" DESTINATION Node Idx {0} does not contain the requested transport mode '{1}'.",destinationNode.Idx,TransportModes.SingleMaskToString(finalTransportMode));

                    var inboundEdgeTypes = destinationNode.GetInboundEdgeTypes();
                    string inboundEdgeTypesS = "";
                    foreach(var edgeType in inboundEdgeTypes)
                    {
                        if(TransportModes.OSMTagIdToKeyValue.ContainsKey(edgeType))
                            inboundEdgeTypesS += edgeType.ToString() +  " " + TransportModes.OSMTagIdToKeyValue[edgeType] + " ";
                    }
                    logger.Debug(" Inbound Edge type(s): {0}",inboundEdgeTypesS);
                    
                    var inboundTransportModes = destinationNode.GetAvailableInboundTransportModes();
                    logger.Debug(" Available inbound transport modes at destination: {0}",TransportModes.MaskToString(inboundTransportModes));
                    finalTransportMode = TransportModes.MaskToArray(inboundTransportModes).First();
                }
            
                var newSequence = new string[2];
                newSequence[0] = TransportModes.SingleMaskToString(initialTransportMode);
                newSequence[1] = TransportModes.SingleMaskToString(finalTransportMode);
                logger.Debug(" Requested transport sequence overridden to: {0}", TransportModes.NamesToString(newSequence));
                
                return newSequence;
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
                        var homeX = persona.HomeLocation!.X;
                        var homeY = persona.HomeLocation.Y;
                        
                        var workX = persona.WorkLocation!.X;
                        var workY = persona.WorkLocation.Y;
                        
                        var requestedTransportModes = persona.RequestedTransportSequence;
                        //var firstMode = requestedTransportModes[0];

                        TimeSpan currentTime = TimeSpan.Zero;

                        List<Node> route = null!;

                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

                        if(origin == destination)
                        {
                            logger.Debug("Origin and destination nodes are equal for Persona Id {0}", persona.Id);

                            persona.Route = routingAlgorithm.TwoPointLineString(homeX, homeY, workX, workY, TransportModes.DefaultMode, currentTime);

                            if(persona.Route.IsEmpty)
                            {
                                logger.Debug("Route is empty for Persona Id {0} !!!!", persona.Id);
                                originEqualsDestinationErrors++;
                                continue;
                            }

                            persona.TransportModeTransitions = routingAlgorithm.SingleTransportModeTransition(origin, destination, TransportModes.DefaultMode);

                            persona.TTextTransitions = SingleTransportTransitionsToTTEXTSequence(persona.Route, persona.TransportModeTransitions);

                            persona.SuccessfulRouteComputation = true;

                            Interlocked.Increment(ref computedRoutes);
                        }
                        else
                        {
                            route = routingAlgorithm.GetRoute(origin, destination, requestedTransportModes);
                        }

                        if(route != null)
                        {
                            if(route.Count > 0)
                            {
                                persona.TransportModeTransitions = routingAlgorithm.GetTransportModeTransitions();

                                //debug:
                                Console.WriteLine("--------------------------------------------------------------------------------------------------------------------------------");
                                int count=0;
                                foreach(var transition in persona.TransportModeTransitions)
                                {
                                    Console.WriteLine("count: {0,3}   TRANSITION::: node idx: {1,7} :: outbound transport mode(s): {2,10} :: outbound route type: {3,3}",
                                    //Console.WriteLine("{0,3}   TRANSITION::: idx: {1,7} :: tm: {2,10} :: rt: {3,3}",
                                                            count++,                 transition.Key,
                                                                                TransportModes.SingleMaskToString(transition.Value.Item1),
                                                                                                                                                transition.Value.Item2);
                                }
                                //

                                persona.Route = routingAlgorithm.NodeRouteToLineStringMSeconds(homeX, homeY, workX, workY, route, currentTime);

                                persona.TTextTransitions = TransportTransitionsToTTEXTSequence(persona.Route, persona.TransportModeTransitions);

                                persona.SuccessfulRouteComputation = true;

                                Interlocked.Increment(ref computedRoutes);
                            }
                            else
                            {
                                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
                                
                                //DEBUG:
                                TracePersonaDetails(persona);
                                logger.Debug("");
                                //
                            }
                        }

                        //debug:
                        var transports = persona.TTextTransitions.Item1;
                        var timestamps = persona.TTextTransitions.Item2;
                        logger.Debug("timestamp:\ttransport:");
                        for(int j=0; j<transports.Length; j++)
                        {
                            logger.Debug("{0}\t{1}",timestamps[j],transports[j]);
                        }                        
                        // TestBench.ExposeTransportTransitionsNodeSeries(route!,persona);
                        // TestBench.ExposeTransportTransitionsTimeSeries(route!,persona);
                        TestBench.ExposeTransportTransitions(route!,persona);
                        //
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
                    logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}. Table {1}", persona.Id, routeTable);
                    uploadFails++;
                }
            }
   
            await connection.CloseAsync();

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", personas.Count - uploadFails, routeTable, totalTime);
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
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routeTable + " (id, transport_sequence, computed_route) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET transport_sequence = $2, computed_route = $3", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = TransportModes.ArrayToNames(persona.RequestedTransportSequence)},
                            new() { Value = persona.Route },
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();

                    var route = persona.Route;
                    double lastTime = -1.0;
                    if(route is not null)
                    {
                        var routeCoordinates=route.Coordinates;
                        lastTime = routeCoordinates.Last().M;
                    }
                        
                    var transportModes = persona.TTextTransitions.Item1;
                    var timeStampsTZ = persona.TTextTransitions.Item2;
                    var interval = TimeSpan.FromSeconds(lastTime);
                    
                    await using var cmd_insert_ttext = new NpgsqlCommand("INSERT INTO " + routeTable + " (id, transport_modes, time_stamps, total_time, total_time_interval) VALUES ($1, $2, $3, $4, $5) ON CONFLICT (id) DO UPDATE SET transport_modes = $2, time_stamps = $3, total_time = $4, total_time_interval = $5", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = transportModes },
                            new() { Value = timeStampsTZ },
                            new() { Value = timeStampsTZ.Last() },
                            new() { Value = interval },
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

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + routeTable + " SET computed_route_temporal_point = computed_route::tgeompoint WHERE is_valid_route = true;", connection))
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
            logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);
            logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", personas.Count - uploadFails, routeTable, totalTime);
            logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personas.Count, 100.0 * (double)uploadFails / (double)personas.Count);
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
            logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)personas.Count);
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
            logger.Debug("Requested transport modes: {0} ({1})", TransportModes.NamesToString(TransportModes.ArrayToNames(persona.RequestedTransportSequence)), TransportModes.ArrayToMask(persona.RequestedTransportSequence));
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
                timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                routeNodeString += String.Format(" {0,14},", node.OsmID);
                routeTimeStampString += String.Format(" {0,14},", timeStamp);
                if(n>2)
                {
                    break;
                }  
            }
            node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[route.Count -1].X, routeCoordinates[route.Count -1].Y);
            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M)); // <- debug: check units
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
            logger.Debug(String.Format("       :                  X ::                  Y ::                      ::                     ::                   "));

            double previousM=-1.0;
            double  currentM=0.0;
            for(var n = 0; n < routeCoordinates.Length; n++)
            {
                currentM = routeCoordinates[n].M;
                node = _graph.GetNodeByLongitudeLatitude(routeCoordinates[n].X, routeCoordinates[n].Y);
                if(route.Coordinates[n].M<double.MaxValue)
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
                else
                    timeStamp = "Inf <<<===";
                if(previousM>=currentM)
                    timeStamp = " " + timeStamp + " <<<=== M ordinate inconsistency"; 
                logger.Debug(String.Format("{0,6} : {1,18} :: {2,18} :: {3,20} :: {4,20} :: {5,15}",n+1,routeCoordinates[n].X,routeCoordinates[n].Y, node.OsmID, routeCoordinates[n].M, timeStamp));
                previousM = currentM;
            }

            //Environment.Exit(0);
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
                            timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[n].M)); // <- debug: check units
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
                    timeStamp = Helper.FormatElapsedTime(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M)); // <- debug: check units
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
            if(transitions == null || transitions.Count <1 || route.IsEmpty)
                return new Tuple<string[],DateTime[]>(new string[0], new DateTime[0]);

            var coordinates = route.Coordinates;
            Node node;
            
            List<DateTime> timeStamps = new List<DateTime>(transitions.Count);
            List<string> transportModes = new List<string>(transitions.Count);

            string defaultMode = TransportModes.SingleMaskToString(TransportModes.DefaultMode);

            byte previousTransportMode = TransportModes.None;
            byte currentTransportMode = TransportModes.DefaultMode;

            string transportModeS = TransportModes.SingleMaskToString(currentTransportMode);

            timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[0].M))); // <- debug: check units
            transportModes.Add(transportModeS);

            for(var n = 1; n < coordinates.Length-1; n++)
            {
                node = _graph.GetNodeByLongitudeLatitude(coordinates[n].X, coordinates[n].Y);

                if(transitions.ContainsKey(node.Idx))
                {
                    previousTransportMode = currentTransportMode;
                    currentTransportMode = transitions[node.Idx].Item1;

                    if(previousTransportMode!=currentTransportMode)
                    {
                        var routeType = transitions[node.Idx].Item2;
                        if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                            transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
                        else
                            transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                        
                        timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[n].M))); // <- debug: check units
                        transportModes.Add(transportModeS);
                    }
                }
            }

            if((currentTransportMode & TransportModes.DefaultMode) == 0)
            {
                timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[route.Count-2].M))); // <- debug: check units
                transportModes.Add(defaultMode);
            }

            timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[route.Count-1].M))); // <- debug: check units
            transportModes.Add(defaultMode);

            return new Tuple<string[],DateTime[]>(transportModes.ToArray(), timeStamps.ToArray());
        }

        private Tuple<string[],DateTime[]> SingleTransportTransitionsToTTEXTSequence(LineString route, Dictionary<int,Tuple<byte,int>> transitions)
        {
            if(transitions == null || transitions.Count <1 || route.IsEmpty)
                return new Tuple<string[],DateTime[]>(new string[0], new DateTime[0]);

            var coordinates = route.Coordinates;

            List<DateTime> timeStamps = new List<DateTime>(transitions.Count);
            List<string> transportModes = new List<string>(transitions.Count);

            Node origin = _graph.GetNodeByLongitudeLatitude(coordinates[0].X, coordinates[0].Y);
        
            string transportModeS = "";
                        
            byte currentTransportMode = 0;    

            if(transitions.ContainsKey(origin.Idx))
            {
                currentTransportMode = transitions[origin.Idx].Item1;
                var routeType = transitions[origin.Idx].Item2;
                if(routeType==-1)
                    transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                else if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
                timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[0].M))); //DEBUG: CHECK UNITS!
                transportModes.Add(transportModeS);                    
            }
            
            Node destination = _graph.GetNodeByLongitudeLatitude(coordinates[route.Count -1].X, coordinates[route.Count -1].Y);

            timeStamps.Add(Constants.BaseDateTime.Add(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M))); //DEBUG: CHECK UNITS!

            if(transitions.ContainsKey(destination.Idx))
            {
                var routeType = transitions[destination.Idx].Item2;
                if(routeType==-1)
                    transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                else if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
            }
            transportModes.Add(transportModeS);

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
