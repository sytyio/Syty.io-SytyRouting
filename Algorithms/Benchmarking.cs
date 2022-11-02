using System.Diagnostics;
using NetTopologySuite.Geometries;
using NLog;
using SytyRouting.Algorithms;
using SytyRouting.Model;

namespace SytyRouting
{
    public class Benchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();   
       
        public static void PointLocationTest(Graph graph)
        {
            logger.Info("Testing point location");
            graph.TestClosestNode("Synapsis",4.369293555585981, 50.82126481464596);
            graph.TestClosestNode("Robinson", 4.3809799, 50.8045279);       
        }

        public static void RoutingAlgorithmBenchmarking<T>(Graph graph, byte transportMode) where T: IRoutingAlgorithm, new()
        {
            var routingAlgorithm = new T();
            routingAlgorithm.Initialize(graph);
            var numberOfNodes = graph.GetNodeCount();
            var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using {0}'s algorithm", routingAlgorithm.GetType().Name);

            logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            RoutingAlgorithmRunTime(routingAlgorithm, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889), transportMode);
            logger.Info("SECOND Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            RoutingAlgorithmRunTime(routingAlgorithm, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889), transportMode);

            logger.Info("Average run time using random origin and destination Nodes in {0} trials:", numberOfRuns);
            RandomSourceTargetRouting(graph, routingAlgorithm, transportMode, numberOfNodes, numberOfRuns);
        }

        public static void MultipleRoutingAlgorithmsBenchmarking<T, U>(Graph graph, byte transportMode) where T: IRoutingAlgorithm, new() where U: IRoutingAlgorithm, new()
        {
            Stopwatch benchmarkStopWatch = new Stopwatch();
            benchmarkStopWatch.Start();

            var algorithm1 = new T();
            var algorithm2 = new U();

            algorithm1.Initialize(graph);
            algorithm2.Initialize(graph);

            var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using RoutingAlgorithm's algorithm");

            logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            var originNodeOsmId = 26913024;          // Synapsis
            var destinationNodeOsmId = 1261889889;   // De Panne Markt, De Panne
            
            var route1 = RoutingAlgorithmRunTime(algorithm1, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId), transportMode);
            var route2 = RoutingAlgorithmRunTime(algorithm2, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId), transportMode);
            
            logger.Info("Output comparison of {0} vs. {1}:", algorithm1.GetType().Name, algorithm2.GetType().Name);
            CompareRoutesSideBySide(route1, route2);
            CompareRouteCostsSideBySide(route1, algorithm1.GetRouteCost(), route2, algorithm2.GetRouteCost());
   
            
            logger.Info("Estimating the average run time using random origin and destination Nodes in {0} trial(s):", numberOfRuns);
            MultipleRandomSourceTargetRouting(graph, algorithm1, algorithm2, transportMode, numberOfRuns);

            benchmarkStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(benchmarkStopWatch.Elapsed);
            logger.Info("Benchmark performed in {0} (HH:MM:S.mS)", totalTime);
        }

        private static List<Node> RoutingAlgorithmRunTime(IRoutingAlgorithm routingAlgorithm, Node origin, Node destination, byte transportMode)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID, transportMode);
            var xympRoute = routingAlgorithm.ConvertRouteFromNodesToLineString(route, TimeSpan.Zero);
            stopWatch.Stop();

            logger.Info("{0,25} execution time: {1,10:0.000} (ms)", routingAlgorithm.GetType().Name, stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000.0);

            return route;
        }

        private static void RandomSourceTargetRouting(Graph graph, IRoutingAlgorithm routingAlgorithm, byte transportMode, int numberOfNodes, int numberOfRuns)
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
                var route = routingAlgorithm.GetRoute(originNode.OsmID, destinationNode.OsmID, transportMode);
                var xympRoute = routingAlgorithm.ConvertRouteFromNodesToLineString(route, TimeSpan.Zero);
                stopWatch.Stop();
                
                elapsedRunTimeTicks[i] = stopWatch.ElapsedTicks;
                logger.Info("RoutingAlgorithm execution time : {0:0} (ms / route)", elapsedRunTimeTicks[i] * nanosecondsPerTick / 1000000);
            }

            var averageTicks = elapsedRunTimeTicks.Average();
            
            logger.Info("{0,25} average execution time: {1,10:0} (ms / route) over {2} trial(s)", routingAlgorithm.GetType().Name, averageTicks * nanosecondsPerTick / 1000000.0, numberOfRuns);
        }

        private static void MultipleRandomSourceTargetRouting(Graph graph, IRoutingAlgorithm algorithm1, IRoutingAlgorithm algorithm2, byte transportMode, int numberOfRuns)
        {
            // var seed = 100100;
            // Random randomIndex = new Random(seed);
            Random randomIndex = new Random();
            
            Stopwatch stopWatch = Stopwatch.StartNew();
            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;
            long[] elapsedRunTimeTicks1 = new long[numberOfRuns];
            long[] elapsedRunTimeTicks2 = new long[numberOfRuns];

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
                var route1 = algorithm1.GetRoute(originNode.OsmID, destinationNode.OsmID, transportMode);
                var xympRoute1 = algorithm1.ConvertRouteFromNodesToLineString(route1, TimeSpan.Zero);
                elapsedRunTimeTicks1[i] = stopWatch.ElapsedTicks-startTicks;

                startTicks = stopWatch.ElapsedTicks;
                var route2 = algorithm2.GetRoute(originNode.OsmID, destinationNode.OsmID, transportMode);
                var xympRoute2 = algorithm2.ConvertRouteFromNodesToLineString(route2, TimeSpan.Zero);
                elapsedRunTimeTicks2[i] = stopWatch.ElapsedTicks-startTicks;

                var routesAreEqual = CompareRouteSequences(route1, route2);
                if(!routesAreEqual)
                {
                    numberOfRouteMismatches++;
                    logger.Debug("{0} and {1} routes are not equal for origin OsmId {2} and destination OsmId {3}.\tRuns: {4},\tMismatches: {5}", algorithm1.GetType().Name, algorithm2.GetType().Name, originNode.OsmID, destinationNode.OsmID, i+1, numberOfRouteMismatches);
                }
                    
                if(numberOfRuns > 10)
                    Console.Write("Run {0,5}\b\b\b\b\b\b\b\b\b", i);
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