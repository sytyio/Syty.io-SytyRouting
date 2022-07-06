using System.Diagnostics;
using NLog;
using SytyRouting.Algorithms.BackwardDijkstra;
using SytyRouting.Algorithms.BidirectionalDijkstra;
using SytyRouting.Algorithms.Dijkstra;
using SytyRouting.Model;

namespace SytyRouting
{
    public class Benchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();   

        public static void MultipleDijkstraBenchmarking(Graph graph)
        {
            Stopwatch benchmarkStopWatch = new Stopwatch();
            benchmarkStopWatch.Start();

            var dijkstra = new Dijkstra(graph);
            var backwardDijkstra = new BackwardDijkstra(graph);
            var bidirectionalDijkstra = new BidirectionalDijkstra(graph);

            // var numberOfRuns = 2000*10; // 07:23:41.830
            var numberOfRuns = 30; // 

            
            // Selected Nodes:
            // From: Synapsis                            (4.369293555585981, 50.82126481464596) : 26913024
            // To: Robinson                              (4.3809799, 50.8045279) : 5137607046
            // To: Place Bara                            (4.3360253, 50.8396486) : 9331990615
            // To: National Basilica of the Sacred Heart (4.3178727, 50.8667117) : 3332515493
            // To: Kasteel van Beersel, Beersel          (4.3003831, 50.7664786) : 249542451
            // To: Sint-Niklaaskerk, Liedekerke          (4.0827609, 50.8706934) : 1241290630
            // To: De Panne Markt, De Panne              (2.5919885, 51.0990340) : 1261889889
            
            // logger.Debug("Synapsis Node OsmId:                              {0}", graph.GetNodeByLatitudeLongitude(4.369293555585981, 50.82126481464596).OsmID);
            // logger.Debug("Robinson Node OsmId:                              {0}", graph.GetNodeByLatitudeLongitude(4.3809799, 50.8045279).OsmID);
            // logger.Debug("Place Bara Node OsmId:                            {0}", graph.GetNodeByLatitudeLongitude(4.3360253, 50.8396486).OsmID);
            // logger.Debug("National Basilica of the Sacred Heart Node OsmId: {0}", graph.GetNodeByLatitudeLongitude(4.3178727, 50.8667117).OsmID);
            // logger.Debug("Kasteel van Beersel, Beersel, Node OsmId:         {0}", graph.GetNodeByLatitudeLongitude(4.3003831, 50.7664786).OsmID);
            // logger.Debug("Sint-Niklaaskerk, Liedekerke, Node OsmId:         {0}", graph.GetNodeByLatitudeLongitude(4.0827609, 50.8706934).OsmID);
            // logger.Debug("De Panne Markt, De Panne, Node OsmId:             {0}", graph.GetNodeByLatitudeLongitude(2.5919885, 51.0990340).OsmID);


            // Well-behaved routes for forward and backward Dijkstra                                  || Bidirectional ?

            // var originNodeOsmId = 26913024;          // Synapsis
            // // var destinationNodeOsmId = 1486032529; // Middle Traffic Light at Avenue Louise 379    || Ok
            // // var destinationNodeOsmId = 7911022011; // IESSID Parking lot                           || Ok
            // // var destinationNodeOsmId = 401454717;  // VUB                                          || Ok
            // // var destinationNodeOsmId = 5137607046; //  Robinson                                    || Ok
            // // var destinationNodeOsmId = 9331990615; //  Place Bara                                  || Ok
            // // var destinationNodeOsmId = 3332515493; //  National Basilica of the Sacred Heart       || Ok
            
            // // var destinationNodeOsmId = 249542451;  //  Kasteel van Beersel, Beersel                || Ok
            // // var destinationNodeOsmId = 1241290630; //  Sint-Niklaaskerk, Liedekerke                || Ok
            // var destinationNodeOsmId = 1261889889; // De Panne Markt, De Panne                     || Ok




            // // Problematic routes for forward and backward Dijkstra:
            
            // // var originNodeOsmId = 1585738565;        // Rue d'Havré, Havré
            // // var destinationNodeOsmId = 3829602659;   // Avenue de la Hêtraie, Braine-le-Comte
            
            // // var originNodeOsmId = graph.GetNodeByIndex(844225).OsmID;        // ?
            // // var destinationNodeOsmId = graph.GetNodeByIndex(946593).OsmID;   // ?

            // // var originNodeOsmId = graph.GetNodeByIndex(844225).OsmID;        // ?
            // // var destinationNodeOsmId = graph.GetNodeByIndex(238058).OsmID;   // ?

            // // var originNodeOsmId = 1904589718;
            // // var destinationNodeOsmId = 3134221830;

            // // var originNodeOsmId =3848587943;
            // // var destinationNodeOsmId = 7242683946;
            
            // // long[] destinationNodeOsmIds = new long[] {1486032529, 7911022011, 401454717, 5137607046, 9331990615, 3332515493, 249542451, 1241290630, 1261889889};



            // // Problematic routes for bidirenctional Dijkstra: Not yet found
               


            // logger.Debug("Origin Node: {0},\tDestination Node {1}. (From Synapsis to De Panne)", originNodeOsmId, destinationNodeOsmId);

            // var routeDijkstra =     DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // // dijkstra.TraceRoute();

            // var routeBackwardDijkstra = BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // // backwardDijkstra.TraceRoute();

            // var routeBidirectionalDijkstra = BidirectionalDijkstraRunTime(bidirectionalDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // // bidirectionalDijkstra.TraceRoute();
            


            // logger.Debug("Comparison Forward vs. Backward");
            // CompareRoutes(routeDijkstra, routeBackwardDijkstra);

            // logger.Debug("Comparison Forward vs. Bidirectional");
            // CompareRoutes(routeDijkstra, routeBidirectionalDijkstra);

            // // logger.Debug("Comparison Backward vs. Bidirectional");
            // // CompareRoutes(routeBackwardDijkstra, routeBidirectionalDijkstra);

            // logger.Debug("Comparison Backward vs. Bidirectional (Reverse)");
            // routeBackwardDijkstra.Reverse();
            // routeBidirectionalDijkstra.Reverse();
            // CompareRoutes(routeBackwardDijkstra, routeBidirectionalDijkstra);

            
            logger.Info("Estimating the average run time using random origin and destination Nodes in {0} trial(s):", numberOfRuns);
            RandomSourceTargetRoutingDijkstra(graph, dijkstra, backwardDijkstra, bidirectionalDijkstra, numberOfRuns);
            

            benchmarkStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(benchmarkStopWatch.Elapsed);
            logger.Info("Benchmark performed in {0} (HH:MM:S.mS)", totalTime);
        }

        private static void RandomSourceTargetRoutingDijkstra(Graph graph, Dijkstra dijkstra, BackwardDijkstra backwardDijkstra, BidirectionalDijkstra bidirectionalDijkstra, int numberOfRuns)
        {
            // var seed = 100100;
            // Random randomIndex = new Random(seed);
            Random randomIndex = new Random();
            
            Stopwatch stopWatch;
            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;
            long[] elapsedDijkstraRunTimeTicks = new long[numberOfRuns];
            long[] elapsedBackwardDijkstraRunTimeTicks = new long[numberOfRuns];
            long[] elapsedBidirectionalDijkstraRunTimeTicks = new long[numberOfRuns];

            var numberOfNodes = graph.GetNodeCount();
            Node originNode;
            Node destinationNode;

            int numberOfRouteMismatchesForwardVsBackward = 0;
            int numberOfRouteMismatchesForwardVsBidirectional = 0;
            int numberOfRouteMismatchesBackwardVsBidirectional = 0;

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

                stopWatch = Stopwatch.StartNew();
                var dijkstraRoute = dijkstra.GetRoute(originNode.OsmID, destinationNode.OsmID);
                stopWatch.Stop();
                elapsedDijkstraRunTimeTicks[i] = stopWatch.ElapsedTicks;

                stopWatch = Stopwatch.StartNew();
                var backwardDijkstraRoute = backwardDijkstra.GetRoute(originNode.OsmID, destinationNode.OsmID);
                stopWatch.Stop();
                elapsedBackwardDijkstraRunTimeTicks[i] = stopWatch.ElapsedTicks;

                stopWatch = Stopwatch.StartNew();
                var bidirectionalDijkstraRoute = bidirectionalDijkstra.GetRoute(originNode.OsmID, destinationNode.OsmID);
                stopWatch.Stop();
                elapsedBidirectionalDijkstraRunTimeTicks[i] = stopWatch.ElapsedTicks;


                logger.Trace("Comparing Dijkstra vs. Backward Dijkstra routes:");
                var routesAreEqual = CompareRoutes(dijkstraRoute, backwardDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesForwardVsBackward++;
                    logger.Debug("Routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesForwardVsBackward);
                }
                
                logger.Trace("Comparing Dijkstra vs. Bidirectional Dijkstra routes:");
                routesAreEqual = CompareRoutes(dijkstraRoute, bidirectionalDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesForwardVsBidirectional++;
                    logger.Debug("Routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesForwardVsBidirectional);
                }

                logger.Trace("Comparing Backward Dijkstra vs. Bidirectional Dijkstra routes:");
                routesAreEqual = CompareRoutes(backwardDijkstraRoute, bidirectionalDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesBackwardVsBidirectional++;
                    logger.Debug("Routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesBackwardVsBidirectional);
                }
                
                Console.Write("Run {0,5}\b\b\b\b\b\b\b\b\b", i);
            }

            if(numberOfRouteMismatchesForwardVsBackward > 0 || numberOfRouteMismatchesForwardVsBidirectional >0 || numberOfRouteMismatchesBackwardVsBidirectional >0)
            {
                logger.Debug("Mismatch route pairs errors (Forward vs. Backward): {0} in {1} trials", numberOfRouteMismatchesForwardVsBackward, numberOfRuns);
                logger.Debug("Mismatch route pairs errors (Forward vs. Bidirectional): {0} in {1} trials", numberOfRouteMismatchesForwardVsBidirectional, numberOfRuns);
                logger.Debug("Mismatch route pairs errors (Backward vs. Bidirectional): {0} in {1} trials", numberOfRouteMismatchesBackwardVsBidirectional, numberOfRuns);
            }
            else
            {
                logger.Debug("No discrepancies found between calclulated route pairs");
            }

            var averageTicksDijkstra = elapsedDijkstraRunTimeTicks.Average();
            logger.Info("        Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);

            var averageTicksBackwardDijkstra = elapsedBackwardDijkstraRunTimeTicks.Average();
            logger.Info("BackwardDijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksBackwardDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);

            var averageTicksBidirectionalDijkstra = elapsedBidirectionalDijkstraRunTimeTicks.Average();
            logger.Info("BidirectionalDijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksBidirectionalDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);
        }

        private static List<Node> DijkstraRunTime(Dijkstra dijkstra, Node origin, Node destination)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            var route = dijkstra.GetRoute(origin.OsmID, destination.OsmID);
            stopWatch.Stop();

            logger.Info("             Dijkstra execution time: {0:0.000} (ms)", stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000);

            return route;
        }

        private static List<Node> BackwardDijkstraRunTime(BackwardDijkstra backwardDijkstra, Node origin, Node destination)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            var route = backwardDijkstra.GetRoute(origin.OsmID, destination.OsmID);
            stopWatch.Stop();

            logger.Info("     BackwardDijkstra execution time: {0:0.000} (ms)", stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000);

            return route;
        }

        private static List<Node> BidirectionalDijkstraRunTime(BidirectionalDijkstra bidirectionalDijkstra, Node origin, Node destination)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            var route = bidirectionalDijkstra.GetRoute(origin.OsmID, destination.OsmID);
            stopWatch.Stop();

            logger.Info("BidirectionalDijkstra execution time: {0:0.000} (ms)", stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000);

            return route;
        }

        private static bool CompareRoutes(List<Node> firstRoute, List<Node> secondRoute)
        {
            var result = Enumerable.SequenceEqual(firstRoute, secondRoute);
            if(result)
            {
                logger.Trace(" => Routes are equal.");
            }
            else
            {
                var maxNumberOfCalculatedNodes = (firstRoute.Count >= secondRoute.Count)? firstRoute.Count : secondRoute.Count;
                logger.Trace(" => Routes are not equal.");
                logger.Trace("    Routes side by side:");
                for(int i = 0; i < maxNumberOfCalculatedNodes; i++)
                {
                        
                    string firstRouteNodeOsmId;
                    if( i >= firstRoute.Count)
                        firstRouteNodeOsmId = "(Empty)";
                    else
                        firstRouteNodeOsmId = firstRoute[i].OsmID.ToString();

                    string secondRouteNodeOsmId;
                    if( i >= secondRoute.Count)
                        secondRouteNodeOsmId = "(Empty)";
                    else
                        secondRouteNodeOsmId = secondRoute[i].OsmID.ToString();

                    logger.Trace("{0} : {1}", firstRouteNodeOsmId, secondRouteNodeOsmId);
                }
            }
            return result;
        }
    }
}