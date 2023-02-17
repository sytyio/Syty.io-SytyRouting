using System.Diagnostics;
using NetTopologySuite.Geometries;
using NetTopologySuite.Utilities;
using NLog;
using Npgsql;
using SytyRouting.Algorithms;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public class RouteUploadBenchmarking
    {
        private static Graph _graph = null!;
        //private string _routeTable;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // public RouteUploadBenchmarking(Graph graph, string routeTable)
        // {
        //     _graph = graph;
        //     _routeTable = routeTable;
        //     //_auxiliaryTable = routeTable+Configuration.AuxiliaryTableSuffix;
        // }



        public static async Task Start<T, U>(Graph graph) where T: IRoutingAlgorithm, new() where U: IRouteUploader, new()
        {
            Stopwatch benchmarkStopWatch = new Stopwatch();
            benchmarkStopWatch.Start();

            _graph = graph;

            var algorithm = new T();
            var uploader = new U();

            algorithm.Initialize(graph);
            // algorithm2.Initialize(graph);

            
            var routeTable = Configuration.PersonaRouteTable;
            var auxiliaryTable = routeTable+Configuration.AuxiliaryTableSuffix;

            // // Persona spatial data generation
            //await RoutingBenchmark.CreateDataSet();
            var personaRouter = new PersonaRouter(graph, routeTable);
            //var personaRouter = new PersonaRouterBenchmark(graph);

            await personaRouter.StartRouting<T>();

            var personas = personaRouter.Personas;
            var computedRoutes = personaRouter.ComputedRoutes;

            await CheckUploadedRoutesAsync(personas, auxiliaryTable, computedRoutes);
    
            //personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();
            
            

            benchmarkStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(benchmarkStopWatch.Elapsed);
            logger.Info("Benchmark performed in {0} (HH:MM:S.mS)", totalTime);
        }

        private static async Task CheckUploadedRoutesAsync(List<Persona> personas, string routeTable, int computedRoutes)
        {
            logger.Info("DB uploaded routes integrity check:");
            int numberOfFailures = 0;
            var uploadedPersonas = new Dictionary<int, LineString>(personas.Count);

            //var connectionString = Configuration.X270ConnectionString; // Local DB for testing
            var connectionString = Configuration.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Read location data from 'persona routes'
            //                     0              1             
            var queryString = "SELECT persona_id, computed_route FROM " + routeTable + " ORDER BY persona_id ASC";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var persona_id = Convert.ToInt32(reader.GetValue(0)); // persona_id (int)
                    var route = (LineString)reader.GetValue(1); // route (Point)
                    try
                    {
                        uploadedPersonas.Add(persona_id, route);
                    }
                    catch
                    {
                        logger.Debug("Unable to download route for Persona Id {0}", persona_id);
                    }
                }
            }

            if (computedRoutes != uploadedPersonas.Count)
            {
                logger.Debug("Inconsistent number of routes in the database");
            }
            logger.Debug("Computed routes {0} : {1} Routes in the database", computedRoutes, uploadedPersonas.Count);

            foreach (var persona in personas)
            {
                if (uploadedPersonas.ContainsKey(persona.Id) && persona.Route is not null)
                {
                    try
                    {
                        Assert.IsEquals(uploadedPersonas[persona.Id], persona.Route, "Test failed. Original and Uploaded Routes are not equal");
                    }
                    catch (NetTopologySuite.Utilities.AssertionFailedException e)
                    {
                        logger.Debug("Route equality assertion failed for Persona Id {0}: {1}", persona.Id, e.Message);
                        numberOfFailures++;
                    }
                }
                else
                {
                    logger.Debug("Unable to compare routes for Persona Id {0}", persona.Id);
                    TracePersonaDetails(persona);
                    numberOfFailures++;
                }
            }
            string result;
            if (numberOfFailures != 0)
                result = "FAILED";
            else
                result = "SUCCEEDED";

            logger.Info("DB uploaded route integrity check {0}.", result);

            await connection.CloseAsync();
        }

        private static void TracePersonaDetails(Persona persona)
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

        public void TracePersonasRouteResult(List<Persona> personas)
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

        public void TracePersonas(List<Persona> personas)
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




        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////       

        public static void RoutingAlgorithmBenchmarking<T>(Graph graph, byte[] transportModesSequence) where T: IRoutingAlgorithm, new()
        {
            var routingAlgorithm = new T();
            routingAlgorithm.Initialize(graph);
            var numberOfNodes = graph.GetNodeCount();
            var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using {0}'s algorithm", routingAlgorithm.GetType().Name);

            logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            RoutingAlgorithmRunTime(routingAlgorithm, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889), transportModesSequence);
            logger.Info("SECOND Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            RoutingAlgorithmRunTime(routingAlgorithm, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889), transportModesSequence);

            logger.Info("Average run time using random origin and destination Nodes in {0} trials:", numberOfRuns);
            RandomSourceTargetRouting(graph, routingAlgorithm, transportModesSequence, numberOfNodes, numberOfRuns);
        }

        private static List<Node> RoutingAlgorithmRunTime(IRoutingAlgorithm routingAlgorithm, Node origin, Node destination, byte[] transportModesSequence)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID, transportModesSequence);
            var xympRoute = routingAlgorithm.NodeRouteToLineStringMSeconds(0,0,0,0,route, TimeSpan.Zero,Constants.BaseDateTime); // <- debug: verify start and end coordinates
            stopWatch.Stop();

            logger.Info("{0,25} execution time: {1,10:0.000} (ms)", routingAlgorithm.GetType().Name, stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000.0);

            return (route);
        }

        private static void RandomSourceTargetRouting(Graph graph, IRoutingAlgorithm routingAlgorithm, byte[] transportModesSequence, int numberOfNodes, int numberOfRuns)
        {
            Random randomIndex = new Random();
            
            Node originNode;
            Node destinationNode;

            long[] elapsedRunTimeTicks = new long[numberOfRuns];

            Stopwatch stopWatch;

            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;

            for(int i = 0; i < numberOfRuns; i++)
            {
                logger.Info("Computing route");
                while(true)
                {
                    var index = randomIndex.Next(0, numberOfNodes);
                    originNode = graph.GetNodeByIndex(index);
                    if(originNode.ValidSource)
                    {
                        break;
                    }
                }
                while(true)
                {
                    var index = randomIndex.Next(0, numberOfNodes);
                    destinationNode = graph.GetNodeByIndex(index);
                    if(destinationNode.ValidTarget)
                    {
                        break;
                    }
                }

                stopWatch = Stopwatch.StartNew();
                var route = routingAlgorithm.GetRoute(originNode.OsmID, destinationNode.OsmID, transportModesSequence);
                var xympRoute = routingAlgorithm.NodeRouteToLineStringMSeconds(0,0,0,0,route, TimeSpan.Zero,Constants.BaseDateTime); // <- debug: verify start and end coordinates
                stopWatch.Stop();
                
                elapsedRunTimeTicks[i] = stopWatch.ElapsedTicks;
                logger.Info("RoutingAlgorithm execution time : {0:0} (ms / route)", elapsedRunTimeTicks[i] * nanosecondsPerTick / 1000000);
            }

            var averageTicks = elapsedRunTimeTicks.Average();
            
            logger.Info("{0,25} average execution time: {1,10:0} (ms / route) over {2} trial(s)", routingAlgorithm.GetType().Name, averageTicks * nanosecondsPerTick / 1000000.0, numberOfRuns);
        }

        private static void MultipleRandomSourceTargetRouting(Graph graph, IRoutingAlgorithm algorithm1, IRoutingAlgorithm algorithm2, byte[] transportModesSequence, int numberOfRuns)
        {
            // var seed = 100100;
            // Random randomIndex = new Random(seed);
            Random randomIndex = new Random();
            
            Stopwatch stopWatch = Stopwatch.StartNew();
            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;
            long[] elapsedRunTimeTicks1 = new long[numberOfRuns];
            long[] elapsedRunTimeTicks2 = new long[numberOfRuns];

            var transportMode = transportModesSequence[0];

            var numberOfNodes = graph.GetNodeCount();
            Node originNode;
            Node destinationNode;

            int numberOfRouteMismatches = 0;

            for(int i = 0; i < numberOfRuns; i++)
            {
                while(true)
                {
                    var index = randomIndex.Next(0, numberOfNodes);
                    originNode = graph.GetNodeByIndex(index);
                    if(originNode.ValidSource)
                    {
                        break;
                    }
                }
                while(true)
                {
                    var index = randomIndex.Next(0, numberOfNodes);
                    destinationNode = graph.GetNodeByIndex(index);
                    if(destinationNode.ValidTarget)
                    {
                        break;
                    }
                }

                var startTicks = stopWatch.ElapsedTicks;
                var route1 = algorithm1.GetRoute(originNode.OsmID, destinationNode.OsmID, transportModesSequence);
                var xympRoute1 = algorithm1.NodeRouteToLineStringMSeconds(0,0,0,0,route1, TimeSpan.Zero,Constants.BaseDateTime); // <- debug: verify start and end coordinates
                elapsedRunTimeTicks1[i] = stopWatch.ElapsedTicks-startTicks;

                startTicks = stopWatch.ElapsedTicks;
                var route2 = algorithm2.GetRoute(originNode.OsmID, destinationNode.OsmID, transportModesSequence);
                var xympRoute2 = algorithm2.NodeRouteToLineStringMSeconds(0,0,0,0,route2, TimeSpan.Zero,Constants.BaseDateTime); // <- debug: verify start and end coordinates
                elapsedRunTimeTicks2[i] = stopWatch.ElapsedTicks-startTicks;

                var routesAreEqual = CompareRouteSequences(route1, route2);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatches++;
                    logger.Debug("{0} and {1} routes are not equal for origin OsmId {2} and destination OsmId {3}.\tRuns: {4},\tMismatches: {5}", algorithm1.GetType().Name, algorithm2.GetType().Name, originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatches);
                }
                    
                if(numberOfRuns > 10)
                    logger.Debug("Run {0,5}\b\b\b\b\b\b\b\b\b", i);
            }

            stopWatch.Stop();

            if(numberOfRouteMismatches > 0)
            {
                logger.Debug("Mismatch route pairs errors: {0} in {1} trials", numberOfRouteMismatches, numberOfRuns);
            }
            else
            {
                logger.Debug("No discrepancies found between calclulated route pairs");
            }

            var averageTicks1 = elapsedRunTimeTicks1.Average();
            logger.Info("{0,25} average execution time: {1,10:0.000} (ms / route) over {2} trial(s)", algorithm1.GetType().Name, averageTicks1 * nanosecondsPerTick / 1000000.0, numberOfRuns);

            var averageTicks2 = elapsedRunTimeTicks2.Average();
            logger.Info("{0,25} average execution time: {1,10:0.000} (ms / route) over {2} trial(s)", algorithm2.GetType().Name, averageTicks2 * nanosecondsPerTick / 1000000.0, numberOfRuns);
        }

        private static void CompareRoutesSideBySide(List<Node> firstRoute, List<Node> secondRoute)
        {
            var result = CompareRouteSequences(firstRoute, secondRoute);
            if(result)
            {
                logger.Info(" => Route sequences are equal.");
            }
            else
            {
                logger.Info(" => Route sequences are not equal.");
            }
            
            var maxNumberOfCalculatedNodes = (firstRoute.Count >= secondRoute.Count)? firstRoute.Count : secondRoute.Count;
            logger.Trace("    Route Nodes side by side:");
            for(int i = 0; i < maxNumberOfCalculatedNodes; i++)
            {
                string firstRouteNodeOsmId  = "(Empty)";
                if(i < firstRoute.Count)
                    firstRouteNodeOsmId = firstRoute[i].OsmID.ToString();
                string secondRouteNodeOsmId = "(Empty)";
                if(i < secondRoute.Count)
                    secondRouteNodeOsmId = secondRoute[i].OsmID.ToString();
                string nodeDifferenceMark = "";
                if(!firstRouteNodeOsmId.Equals(secondRouteNodeOsmId))
                    nodeDifferenceMark = "<<==";
                logger.Trace("{0} : {1}\t\t{2}", firstRouteNodeOsmId, secondRouteNodeOsmId, nodeDifferenceMark);
            }
        }

        private static bool CompareRouteSequences(List<Node> firstRoute, List<Node> secondRoute)
        {
            var result = Enumerable.SequenceEqual(firstRoute, secondRoute);
            return result;
        }

        private static void CompareRouteCostsSideBySide(List<Node> firstRoute, double firstRouteNativeCost, List<Node> secondRoute, double secondRouteNativeCost)
        {            
            var firstRouteCost = ForwardRouteCost(firstRoute);
            var secondRouteCost = ForwardRouteCost(secondRoute);
            var costDifference = firstRouteCost-secondRouteCost;
            logger.Info("        Native Costs: {0,25} :: {1,25} :: Difference: {2}", firstRouteNativeCost, secondRouteNativeCost, firstRouteNativeCost - secondRouteNativeCost);
            logger.Info(" Forward route Costs: {0,25} :: {1,25} :: Difference: {2}", firstRouteCost, secondRouteCost, costDifference);
            logger.Info("          Difference: {0,25} :: {1,25} ::", firstRouteNativeCost - firstRouteCost, secondRouteNativeCost - secondRouteCost, costDifference);
        }

        private static bool CompareRouteCosts(List<Node> firstRoute, List<Node> secondRoute)
        {
            var minDeltaCost = 1e-8; // min |cost| from public.ways = 1.0000000116860974e-07
            
            var firstRouteCost = ForwardRouteCost(firstRoute);
            var secondRouteCost = ForwardRouteCost(secondRoute);
            var costDifference = Math.Abs(firstRouteCost-secondRouteCost);

            return (costDifference <= minDeltaCost)? true: false;
        }

        private static double ForwardRouteCost(List<Node> route)
        {
            double cost = 0;
            for(int i = 0; i < route.Count-1; i++)
            {
                var allValidEdges = route[i].OutwardEdges.FindAll(e => e.TargetNode.Idx == route[i+1].Idx);
                var minCost = allValidEdges.Select(e => e.Cost).Min();
                var edge = allValidEdges.Find(e => e.Cost == minCost);

                if(edge is not null)
                    cost = cost + edge.Cost;
                else
                    logger.Debug("Outward Edge not found. Source Node {0}. Target Node {1}", route[i].OsmID, route[i+1].OsmID);
            }

            return cost;
        }
    }
}