using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;
using System.Collections.Concurrent;
//using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.Routing
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<Persona> Personas {private set; get;} = null!;

        private List<Persona> personas = new List<Persona>();
        
        private Graph _graph;
        private string _routeTable;
        private string _auxiliaryTable;

        private static int simultaneousRoutingTasks = Environment.ProcessorCount;

        private Task[] routingTasks = new Task[simultaneousRoutingTasks];

        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

        private int taskArraysQueueThreshold = simultaneousRoutingTasks;

        private int elementsToProcess = 0;
        private int processedDbElements = 0;
        public int ComputedRoutes {private set; get;} = 0;
        private static int computedRoutes = 0;
        private bool routingTasksHaveEnded = false;
    
        private int regularBatchSize = simultaneousRoutingTasks * Configuration.RegularRoutingTaskBatchSize;

        private Stopwatch stopWatch = new Stopwatch();

        private int sequenceValidationErrors = 0;
        private int originEqualsDestinationErrors = 0;

        public PersonaRouter(Graph graph, string routeTable)
        {
            _graph = graph;
            _routeTable = routeTable;
            _auxiliaryTable = routeTable+Configuration.AuxiliaryTableSuffix;
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            elementsToProcess = await Helper.DbTableRowCount(_routeTable, logger);
            //elementsToProcess = 6; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
            //elementsToProcess = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTable, logger);

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

            ComputedRoutes = computedRoutes;
            Personas = personas;

            await UploadRoutesAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("=================================================");
            logger.Info("    Routing execution time :: {0}", totalTime);
            logger.Info("=================================================");
        }

        private async Task DBPersonaDownloadAsync()
        {
            int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var personaTable = _routeTable;

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
                //                        0   1              2              3           4
                var queryString = "SELECT id, home_location, work_location, start_time, requested_transport_modes FROM " + personaTable + " ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)
                        var startTime = (DateTime)reader.GetValue(3); // start_time (TIMESTAMPTZ)
                        var requestedSequence = reader.GetValue(4); // transport_sequence (text[])
                        byte[] requestedTransportSequence;
                        if(requestedSequence is not null && requestedSequence != DBNull.Value)
                        {
                             requestedTransportSequence = ValidateTransportSequence(id, homeLocation, workLocation, (string[])requestedSequence);
                        }
                        else
                        {
                            requestedTransportSequence = new byte[0];
                        }

                        var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation, StartDateTime = startTime, RequestedTransportSequence = requestedTransportSequence};
                        
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

        private byte[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence)
        {
            byte[] formattedSequence = TransportModes.CreateSequence(transportSequence);

            byte initialTransportMode = formattedSequence.First();
            byte finalTransportMode = formattedSequence.Last();

            var originNode = _graph.GetNodeByLongitudeLatitude(homeLocation.X, homeLocation.Y, isSource: true);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(workLocation.X, workLocation.Y, isTarget: true);

            Edge outboundEdge = originNode.GetFirstOutboundEdge(initialTransportMode);
            Edge inboundEdge = destinationNode.GetFirstInboundEdge(finalTransportMode);

            if(outboundEdge != null && inboundEdge != null)
            {
                return formattedSequence;
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
            
                var newSequence = new byte[2];
                newSequence[0] = initialTransportMode;
                newSequence[1] = finalTransportMode;
                logger.Debug(" Requested transport sequence overridden to: {0}", TransportModes.ArrayToNames(newSequence));
                
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

                        TimeSpan initialTime = TimeSpan.Zero;

                        List<Node> route = null!;

                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

                        if(origin == destination)
                        {
                            logger.Debug("Origin and destination nodes are equal for Persona Id {0}", persona.Id);

                            persona.Route = routingAlgorithm.TwoPointLineString(homeX, homeY, workX, workY, TransportModes.DefaultMode, initialTime);

                            if(persona.Route.IsEmpty)
                            {
                                logger.Debug("Route is empty for Persona Id {0} !!!!", persona.Id);
                                originEqualsDestinationErrors++;
                                continue;
                            }

                            //persona.TransportModeTransitions = routingAlgorithm.SingleTransportModeTransition(persona, origin, destination, TransportModes.DefaultMode);

                            persona.TTextTransitions = routingAlgorithm.SingleTransportModeTransition(persona, origin, destination, TransportModes.DefaultMode);
                            //SingleTransportTransitionsToTTEXTSequence(persona.Route, persona.TransportModeTransitions, persona.StartDateTime);

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
                                persona.Route = routingAlgorithm.NodeRouteToLineStringMSeconds(homeX, homeY, workX, workY, route, initialTime, persona.StartDateTime);

                                persona.TTextTransitions = routingAlgorithm.GetTransportModeTransitions();

                                persona.SuccessfulRouteComputation = true;

                                Interlocked.Increment(ref computedRoutes);
                            }
                            else
                            {
                                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
                            }
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

        private async Task UploadRoutesAsync()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;       

            var auxiliaryTable = _auxiliaryTable;
            var routeTable = _routeTable;

            var uploader = new DataBase.OneTimeAllUpload();

            int uploadFails = await uploader.UploadRoutesAsync(connectionString,auxiliaryTable,routeTable,personas);

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);
            logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", personas.Count - uploadFails, auxiliaryTable, totalTime);
            logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personas.Count, 100.0 * (double)uploadFails / (double)personas.Count);
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
            logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)personas.Count);
        }

        private Tuple<string[],DateTime[]> SingleTransportTransitionsToTTEXTSequence(LineString route, Dictionary<int,Tuple<byte,int>> transitions, DateTime startTime)
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
                timeStamps.Add(startTime.Add(TimeSpan.FromSeconds(route.Coordinates[0].M))); //DEBUG: CHECK UNITS!
                transportModes.Add(transportModeS);                    
            }
            
            Node destination = _graph.GetNodeByLongitudeLatitude(coordinates[route.Count -1].X, coordinates[route.Count -1].Y);

            timeStamps.Add(startTime.Add(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M))); //DEBUG: CHECK UNITS!

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
    }
}