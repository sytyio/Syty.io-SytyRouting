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
            var numberOfRuns = 10; // 

            
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

            var originNodeOsmId = 26913024;          // Synapsis
            // var destinationNodeOsmId = 1486032529; // Middle Traffic Light at Avenue Louise 379    || Ok
            // var destinationNodeOsmId = 7911022011; // IESSID Parking lot                           || Ok
            // var destinationNodeOsmId = 401454717;  // VUB                                          || Ok
            // var destinationNodeOsmId = 5137607046; //  Robinson                                    || Ok
            // var destinationNodeOsmId = 9331990615; //  Place Bara                                  || Ok
            // var destinationNodeOsmId = 3332515493; //  National Basilica of the Sacred Heart       || Ok
            
            // var destinationNodeOsmId = 249542451;  //  Kasteel van Beersel, Beersel                || Ok
            // var destinationNodeOsmId = 1241290630; //  Sint-Niklaaskerk, Liedekerke                || Ok
            var destinationNodeOsmId = 1261889889; // De Panne Markt, De Panne                     || Ok




            // Problematic routes for forward and backward Dijkstra:
            
            // var originNodeOsmId = 1585738565;        // Rue d'Havré, Havré                         ||
            // var destinationNodeOsmId = 3829602659;   // Avenue de la Hêtraie, Braine-le-Comte      || One way  Ok
            
            // var originNodeOsmId = graph.GetNodeByIndex(844225).OsmID;        // ?                  ||
            // var destinationNodeOsmId = graph.GetNodeByIndex(946593).OsmID;   // ?                  || One way  Ok

            // var originNodeOsmId = graph.GetNodeByIndex(844225).OsmID;        // ?                  ||
            // var destinationNodeOsmId = graph.GetNodeByIndex(238058).OsmID;   // ?                  || One way  Ok

            // var originNodeOsmId = 1904589718;         //                                             ||
            // var destinationNodeOsmId = 3134221830;    //                                             || One way  Ok

            // var originNodeOsmId = 3848587943;             //                                          ||
            // var destinationNodeOsmId = 7242683946;        //                                          || One way  Ok
            
            // long[] destinationNodeOsmIds = new long[] {1486032529, 7911022011, 401454717, 5137607046, 9331990615, 3332515493, 249542451, 1241290630, 1261889889};



            // Problematic routes for bidirenctional Dijkstra:

            // var originNodeOsmId = 32002195;         //                                               ||
            // var destinationNodeOsmId = 2018575156;  //                                               || Same total cost, + one-node difference : Solved!

            // var originNodeOsmId = 8446443851;       //                                               ||
            // var destinationNodeOsmId = 8887385726;  //                                               || Cost difference: 2.175031300027941E-05, + one-node difference : Solved!

            // var originNodeOsmId = 8788251032;        //                                              ||
            // var destinationNodeOsmId = 4327261354;   //                                              || Cost difference: 2.6458415426922066E-05, + three-node difference : Solved!

            // var originNodeOsmId = 1037661890;        //                                              ||
            // var destinationNodeOsmId = 674283549;    //                                              || Cost difference: 0.005081619700421536, + three-node difference : Solved!

            // var originNodeOsmId = 3688272788;        //                                              ||
             // var originNodeOsmId = 2061554515;
                // Intermediate nodes: 1540198029 : 1540198029  |  3916980725 : 3852544443	|  3916980716 : 3916980716	
                // var originNodeOsmId = 3688272788;       //                                             ||
                // var destinationNodeOsmId = 1540198029;  //                                             || Routes are equal
                    // var originNodeOsmId = 1540198029;       //                                             ||
                    // var destinationNodeOsmId = 3916980716;  //                                             || Routes are equal
                // var originNodeOsmId = 3916980716;       //                                             ||
                // var destinationNodeOsmId = 459594462;   //                                             || Routes are equal
             // var destinationNodeOsmId = 271905863;
            // var destinationNodeOsmId = 459594462;   //                                              || Cost difference: 0.0006598831101641833, same number of nodes, only one node mismatches : Solved!

            // var originNodeOsmId = 3806617624;        //                                              ||
            // var destinationNodeOsmId =  344654801;   //                                              || Cost difference: 9.590737614539879E-05, - five-node difference : Solved!
            
            // var originNodeOsmId = 3366445178;        //                                              ||
            // var destinationNodeOsmId =  5173331215;   //                                             || Cost difference: 0.0010283265373622896, - five-node difference : Solved!

            // var originNodeOsmId = 362751277;          //                                             ||
            // var destinationNodeOsmId =  6140750360;   //                                             || Cost difference: 1.6031788459436314E-05, same number of nodes, only two nodes mismatch : Solved!
            
        
        



            logger.Debug("Origin Node: {0},\tDestination Node {1}. (From Synapsis to De Panne)", originNodeOsmId, destinationNodeOsmId);

            var routeDijkstra =     DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // dijkstra.TraceRoute();

            var routeBackwardDijkstra = BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // backwardDijkstra.TraceRoute();

            var routeBidirectionalDijkstra = BidirectionalDijkstraRunTime(bidirectionalDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // bidirectionalDijkstra.TraceRoute();
            


            logger.Trace("Comparison Forward vs. Backward");
            CompareRouteNodes(routeDijkstra, routeBackwardDijkstra);
            CompareForwardRouteCosts(routeDijkstra, dijkstra.GetRouteCost(), routeBackwardDijkstra, backwardDijkstra.GetRouteCost());
            CompareBackwardRouteCosts(routeDijkstra, dijkstra.GetRouteCost(), routeBackwardDijkstra, backwardDijkstra.GetRouteCost());


            logger.Trace("Comparison Forward vs. Bidirectional");
            CompareRouteNodes(routeDijkstra, routeBidirectionalDijkstra);
            CompareForwardRouteCosts(routeDijkstra, dijkstra.GetRouteCost(), routeBidirectionalDijkstra, bidirectionalDijkstra.GetRouteCost());
            CompareBackwardRouteCosts(routeDijkstra, dijkstra.GetRouteCost(), routeBidirectionalDijkstra, bidirectionalDijkstra.GetRouteCost());
            

            logger.Trace("Comparison Backward vs. Bidirectional");
            CompareRouteNodes(routeBackwardDijkstra, routeBidirectionalDijkstra);
            CompareForwardRouteCosts(routeBackwardDijkstra, backwardDijkstra.GetRouteCost(), routeBidirectionalDijkstra, bidirectionalDijkstra.GetRouteCost());
            CompareBackwardRouteCosts(routeBackwardDijkstra, backwardDijkstra.GetRouteCost(), routeBidirectionalDijkstra, bidirectionalDijkstra.GetRouteCost());

            // Reversed routes:
            // logger.Debug("Comparison Backward vs. Bidirectional (Reverse)");
            // routeBackwardDijkstra.Reverse();
            // routeBidirectionalDijkstra.Reverse();
            // CompareRouteNodes(routeBackwardDijkstra, routeBidirectionalDijkstra);
            // logger.Debug("        Native Costs: {0} :: {1} :: Difference: {2}", backwardDijkstra.GetRouteCost(), bidirectionalDijkstra.GetRouteCost(), backwardDijkstra.GetRouteCost() - bidirectionalDijkstra.GetRouteCost());
            // CompareBackwardRouteCosts(routeBackwardDijkstra, routeBidirectionalDijkstra);

            
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
                var routesAreEqual = CompareRouteNodes(dijkstraRoute, backwardDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesForwardVsBackward++;
                    logger.Debug(" FWD and BKWD routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesForwardVsBackward);
                }
                
                logger.Trace("Comparing Dijkstra vs. Bidirectional Dijkstra routes:");
                routesAreEqual = CompareRouteNodes(dijkstraRoute, bidirectionalDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesForwardVsBidirectional++;
                    logger.Debug(" FWD and BIDR routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesForwardVsBidirectional);
                }

                logger.Trace("Comparing Backward Dijkstra vs. Bidirectional Dijkstra routes:");
                routesAreEqual = CompareRouteNodes(backwardDijkstraRoute, bidirectionalDijkstraRoute);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatchesBackwardVsBidirectional++;
                    logger.Debug("BKWD and BIDR routes are not equal for origin OsmId {0} and destination OsmId {1}.\tRuns: {2},\tMismatches: {3}", originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatchesBackwardVsBidirectional);
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
            logger.Info("             Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);

            var averageTicksBackwardDijkstra = elapsedBackwardDijkstraRunTimeTicks.Average();
            logger.Info("     BackwardDijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksBackwardDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);

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

        private static bool CompareRouteNodes(List<Node> firstRoute, List<Node> secondRoute)
        {
            var result = Enumerable.SequenceEqual(firstRoute, secondRoute);
            if(result)
            {
                logger.Trace(" => Route sequences are equal.");
            }
            else
            {
                var maxNumberOfCalculatedNodes = (firstRoute.Count >= secondRoute.Count)? firstRoute.Count : secondRoute.Count;
                logger.Trace(" => Route sequences are not equal.");
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

            return result;
        }

        private static bool CompareForwardRouteCosts(List<Node> firstRoute, double firstRouteNativeCost, List<Node> secondRoute, double secondRouteNativeCost)
        {
            var minDeltaCost = 1e-8; // min |cost| from public.ways = 1.0000000116860974e-07
            
            var firstRouteCost = ForwardRouteCost(firstRoute);
            var secondRouteCost = ForwardRouteCost(secondRoute);
            var costDifference = firstRouteCost-secondRouteCost;
            logger.Trace("        Native Costs: {0} :: {1} :: Difference: {2}", firstRouteNativeCost, secondRouteNativeCost, firstRouteNativeCost - secondRouteNativeCost);
            logger.Trace(" Forward route Costs: {0} :: {1} :: Difference: {2}", firstRouteCost, secondRouteCost, costDifference);
            logger.Trace("          Difference: {0} :: {1}", firstRouteNativeCost - firstRouteCost, secondRouteNativeCost - secondRouteCost, costDifference);

            if(costDifference <= minDeltaCost)
                return true;
            else
                return false;
        }

        private static bool CompareBackwardRouteCosts(List<Node> firstRoute, double firstRouteNativeCost, List<Node> secondRoute, double secondRouteNativeCost)
        {
            var minDeltaCost = 1e-8; // min |cost| from public.ways = 1.0000000116860974e-07
            
            var firstRouteCost = BackwardRouteCost(firstRoute);
            var secondRouteCost = BackwardRouteCost(secondRoute);
            var costDifference = firstRouteCost-secondRouteCost;
            logger.Trace("        Native Costs: {0} :: {1} :: Difference: {2}", firstRouteNativeCost, secondRouteNativeCost, firstRouteNativeCost - secondRouteNativeCost);
            logger.Trace("Backward route Costs: {0} :: {1} :: Difference: {2}", firstRouteCost, secondRouteCost, costDifference);
            logger.Trace("          Difference: {0} :: {1}", firstRouteNativeCost - firstRouteCost, secondRouteNativeCost - secondRouteCost, costDifference);

            if(costDifference <= minDeltaCost)
                return true;
            else
                return false;
        }

        private static double ForwardRouteCost(List<Node> route)
        {
            double cost = 0;
            // logger.Trace("Source Node; Target Node; Cumulated Cost");
            for(int i = 0; i < route.Count-1; i++)
            {
                var allValidEdges = route[i].OutwardEdges.FindAll(e => e.TargetNode.Idx == route[i+1].Idx);
                var minCost = allValidEdges.Select(e => e.Cost).Min();
                var edge = allValidEdges.Find(e => e.Cost == minCost);

                // if(route[i].OsmID == 4729277084)
                // {
                //     logger.Trace("Node {0}", route[i].OsmID);
                // }

                if(edge is not null)
                {
                    cost = cost + edge.Cost;
                    // logger.Trace("{0}; {1}; {2}", route[i].OsmID, route[i+1].OsmID, cost);
                }
                else
                    logger.Debug("Outward Edge not found. Source Node {0}. Target Node {1}", route[i].OsmID, route[i+1].OsmID);
            }
            // logger.Debug("First Node {0},\t Last Node {1}", route[0].OsmID, route[route.Count-1].OsmID);

            return cost;
        }

        private static double BackwardRouteCost(List<Node> route)
        {
            double cost = 0;
            // logger.Trace("Target Node; Source Node; Cumulated Cost");
            for(int i = route.Count-1; i > 0 ; i--)
            {
                var allValidEdges = route[i].InwardEdges.FindAll(e => e.SourceNode.Idx == route[i-1].Idx);
                var minCost = allValidEdges.Select(e => e.Cost).Min();
                var edge = allValidEdges.Find(e => e.Cost == minCost);
                // var edge = route[i].InwardEdges.Find(e => e.SourceNode.Idx == route[i-1].Idx);
                if(edge is not null)
                {
                    cost = cost + edge.Cost;
                    // logger.Trace("{0}; {1}; {2}", route[i].OsmID, route[i-1].OsmID, cost);
                }
                else
                    logger.Debug("Inward Edge not found. Source Node {0}. Source Node {1}", route[i].OsmID, route[i-1].OsmID);
            }
            // logger.Debug("First Node {0},\t Last Node {1}", route[0].OsmID, route[route.Count-1].OsmID);

            return cost;
        }

        // The following two methods work only with a reversed route:
        private static bool CompareBackwardReverseRouteCosts(List<Node> firstRoute, List<Node> secondRoute)
        {
            var minDeltaCost = 1e-8; // min |reverse_cost| from public.ways = 1.0000000116860974e-07
            var firstRouteCost = BackwardReverseRouteCost(firstRoute);
            var secondRouteCost = BackwardReverseRouteCost(secondRoute);
            var costDifference = firstRouteCost-secondRouteCost;
            logger.Trace("Backward route Costs: {0} :: {1}, Cost difference: {2}", firstRouteCost, secondRouteCost, costDifference);

            if(costDifference <= minDeltaCost)
                return true;
            else
                return false;
        }

        private static double BackwardReverseRouteCost(List<Node> route)
        {
            double cost = 0;
            for(int i = 0; i < route.Count-1; i++)
            {
                var edge = route[i].InwardEdges.Find(e => e.SourceNode.Idx == route[i+1].Idx);
                if(edge is not null)
                    cost = cost + edge.Cost;
                else
                    logger.Debug("Inward Edge not found. Source Node {0}. Target Node {1}", route[i].OsmID, route[i+1].OsmID);
            }
            // logger.Debug("First Node {0},\t Last Node {1}", route[0].OsmID, route[route.Count-1].OsmID);

            return cost;
        }
    }
}