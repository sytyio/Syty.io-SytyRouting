using System.Diagnostics;
using NLog;
using SytyRouting.Algorithms.Dijkstra;
using SytyRouting.Model;

namespace SytyRouting
{
    public class Benchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();   

        public static void DijkstraBenchmarking(Graph graph)
        {
            var dijkstra = new Dijkstra(graph);
            var numberOfNodes = graph.GetNodesArraySize();
            // var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using Dijkstra's algorithm");
            // Selected Nodes:
            // dijkstra.GetRoute(2135360285, 145351);
            // dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            // dijkstra.GetRoute(2135360285, -145351);
            // dijkstra.GetRoute(26913029, 401454717);
            // dijkstra.GetRoute(26913024, 1486032529);
            // dijkstra.GetRoute(26913024, 7911022011);

            
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1486032529));
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(7911022011));
            DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));
            dijkstra.TraceRoute();

            

            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            // logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            // logger.Info("Estimating the average run time using random origin and destination Nodes in {0} trial(s):", numberOfRuns);
            // RandomSourceTargetRouting(graph, dijkstra, numberOfNodes, numberOfRuns);
        }

        public static void BackwardDijkstraBenchmarking(Graph graph)
        {
            var backwardDijkstra = new BackwardDijkstra(graph);
            var numberOfNodes = graph.GetNodesArraySize();
            // var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using Backward Dijkstra's algorithm");
            // Selected Nodes:
            // dijkstra.GetRoute(2135360285, 145351);
            // dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            // dijkstra.GetRoute(2135360285, -145351);
            // dijkstra.GetRoute(26913029, 401454717);
            // backwardDijkstra.GetRoute(26913024, 1486032529);
            // backwardDijkstra.GetRoute(26913024, 7911022011);


            // BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1486032529));
            // BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(7911022011));
            BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));
            backwardDijkstra.TraceRoute();

            
            
            
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            // logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            // logger.Info("Estimating the average run time using random origin and destination Nodes in {0} trial(s):", numberOfRuns);
            // RandomSourceTargetRouting(graph, dijkstra, numberOfNodes, numberOfRuns);
        }

        public static void BidirectionalDijkstraBenchmarking(Graph graph)
        {
            var dijkstra = new Dijkstra(graph);
            var backwardDijkstra = new BackwardDijkstra(graph);
            var bidirectionalDijkstra = new BidirectionalDijkstra(graph);
            var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using Bidirectional Dijkstra's algorithm");
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


            // BidirectionalDijkstraRunTime(bidirectionalDijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1486032529));
            // BidirectionalDijkstraRunTime(bidirectionalDijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(7911022011));


            var originNodeOsmId = 26913024;             // Synapsis
            // var destinationNodeOsmId = 1486032529;      // Middle Traffic Light at Avenue Louise 379
            // var destinationNodeOsmId = 7911022011; // IESSID Parking lot
            // var destinationNodeOsmId = 401454717;  // VUB

            // var destinationNodeOsmId = 5137607046; //  Robinson
            // var destinationNodeOsmId = 9331990615; //  Place Bara
            // var destinationNodeOsmId = 3332515493; //  National Basilica of the Sacred Heart
            // var destinationNodeOsmId = 249542451;  //  Kasteel van Beersel, Beersel
            // var destinationNodeOsmId = 1241290630; //  Sint-Niklaaskerk, Liedekerke
            var destinationNodeOsmId = 1261889889; // De Panne Markt

            
            var routeDijkstra = DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // dijkstra.TraceRoute();

            var routeBackwardDijkstra = BackwardDijkstraRunTime(backwardDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // backwardDijkstra.TraceRoute();

            CompareRoutes(routeDijkstra, routeBackwardDijkstra);



            // BidirectionalDijkstraRunTime(bidirectionalDijkstra, graph.GetNodeByOsmId(originNodeOsmId), graph.GetNodeByOsmId(destinationNodeOsmId));
            // bidirectionalDijkstra.TraceRoute();
            

            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            // logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            logger.Info("Estimating the average run time using random origin and destination Nodes in {0} trial(s):", numberOfRuns);
            RandomSourceTargetRoutingTwoAlgorithms(graph, dijkstra, backwardDijkstra, numberOfRuns);
        }

        private static void RandomSourceTargetRouting(Graph graph, Dijkstra dijkstra, int numberOfNodes, int numberOfRuns)
        {
            var seed = 100100;

            Random randomIndex = new Random(seed);
            
            Node originNode;
            Node destinationNode;

            long[] elapsedRunTimeTicks = new long[numberOfRuns];

            Stopwatch stopWatch;

            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;

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
                dijkstra.GetRoute(originNode.OsmID, destinationNode.OsmID);
                stopWatch.Stop();
                
                elapsedRunTimeTicks[i] = stopWatch.ElapsedTicks;
            }

            var averageTicks = elapsedRunTimeTicks.Average();
            logger.Info("Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicks * nanosecondsPerTick / 1000000, numberOfRuns);
        }

        private static void RandomSourceTargetRoutingTwoAlgorithms(Graph graph, Dijkstra dijkstra, BackwardDijkstra backwardDijkstra, int numberOfRuns)
        {
            var seed = 100100;
            Random randomIndex = new Random(seed);

            Stopwatch stopWatch;
            long frequency = Stopwatch.Frequency;
            long nanosecondsPerTick = (1000L*1000L*1000L) / frequency;
            long[] elapsedDijkstraRunTimeTicks = new long[numberOfRuns];
            long[] elapsedBackwardDijkstraRunTimeTicks = new long[numberOfRuns];

            var numberOfNodes = graph.GetNodesArraySize();
            Node originNode;
            Node destinationNode;

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
            }

            var averageTicksDijkstra = elapsedDijkstraRunTimeTicks.Average();
            logger.Info("Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageTicksDijkstra * nanosecondsPerTick / 1000000, numberOfRuns);
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

        private static void CompareRoutes(List<Node> firstRoute, List<Node> secondRoute)
        {
            logger.Debug("Route comparison:");
            var routesAreEqual = Enumerable.SequenceEqual(firstRoute, secondRoute);
            if(routesAreEqual)
            {
                logger.Debug("Routes are equal");
            }
            else
            {
                logger.Debug("Routes side by side:");
                for(int i = 0; i < firstRoute.Count; i++)
                {
                    logger.Debug("{0} : {1}", firstRoute[i].Idx, secondRoute[i].Idx);
                }
            }
        }
    }
}