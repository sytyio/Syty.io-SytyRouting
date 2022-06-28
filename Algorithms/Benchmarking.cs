using System.Diagnostics;
using NLog;
using SytyRouting.Algorithms.Dijkstra;


namespace SytyRouting
{
    public class Benchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();   

        public static void DijkstraBenchmarking(Graph graph)
        {
            var dijkstra = new Dijkstra(graph);
            var numberOfNodes = graph.GetNodesArraySize();
            var numberOfRuns = 1000;

            logger.Info("Route searching benchmarking using Dijkstra's algorithm");
            // dijkstra.GetRoute(2135360285, 145351);
            // dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            // dijkstra.GetRoute(2135360285, -145351);
            // dijkstra.GetRoute(26913029, 401454717);

            // logger.Info("Route searching using Dijkstra's algorithm based on coordinates");
            // From: Synapsis (4.369293555585981, 50.82126481464596)
            // To: Robinson  (4.3809799, 50.8045279)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3809799, 50.8045279);
            // To: Place Bara (4.3360253, 50.8396486)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3360253, 50.8396486);
            // To: National Basilica of the Sacred Heart (4.3178727, 50.8667117)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3178727, 50.8667117);
            // To: Kasteel van Beersel, Beersel (4.3003831, 50.7664786)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3003831, 50.7664786);
            // To: Sint-Niklaaskerk, Liedekerke (4.0827609, 50.8706934)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.0827609, 50.8706934);
            // To: De Panne Markt, De Panne (2.5919885, 51.0990340)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 2.5919885, 51.0990340);

            // logger.Debug("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            // DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            logger.Info("Average run time using random origin and destination Nodes in {0} trials:", numberOfRuns);
            RandomSourceTargetRouting(graph, dijkstra, numberOfNodes, numberOfRuns);

        }

        private static void RandomSourceTargetRouting(Graph graph, Dijkstra dijkstra, int numberOfNodes, int numberOfRuns)
        {
            // var seed = 100100;

            Random randomIndex = new Random();
            
            Node originNode;
            Node destinationNode;

            List<long> elapsedMilliseconds = new List<long>();

            for(long i = 1; i <= numberOfRuns; i++)
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

                logger.Debug("Origin Node     \t OsmId {0}", originNode.OsmID);
                logger.Debug("Destination Node\t OsmId {0}", destinationNode.OsmID);

                var ElapsedMilliseconds = DijkstraRunTime(dijkstra, originNode, destinationNode);
                elapsedMilliseconds.Add(ElapsedMilliseconds);

                if (i % (int)(numberOfRuns * 0.01) == 0)
                {                        
                    var averageMilliseconds = elapsedMilliseconds.Average();
                    logger.Info("Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageMilliseconds, i);    
                }

            }

            var averageMillisecondsTotal = elapsedMilliseconds.Average();
            logger.Info("Dijkstra average execution time: {0:0.000} (ms / route) over {1} trial(s)", averageMillisecondsTotal, numberOfRuns);
        }

        private static long DijkstraRunTime(Dijkstra dijkstra, Node origin, Node destination)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            dijkstra.GetRoute(origin.OsmID, destination.OsmID);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("Route created in {0} (HH:MM:S.mS)", totalTime);

            return stopWatch.ElapsedMilliseconds;
        }
    }
}