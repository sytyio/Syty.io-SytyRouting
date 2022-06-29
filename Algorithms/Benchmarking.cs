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
            var numberOfRuns = 10;

            logger.Info("Route searching benchmarking using Dijkstra's algorithm");

            logger.Info("Route From Synapsis (4.369293555585981, 50.82126481464596) to De Panne Markt, De Panne (2.5919885, 51.0990340)");
            DijkstraRunTime(dijkstra, graph.GetNodeByOsmId(26913024), graph.GetNodeByOsmId(1261889889));

            logger.Info("Average run time using random origin and destination Nodes in {0} trials:", numberOfRuns);
            RandomSourceTargetRouting(graph, dijkstra, numberOfNodes, numberOfRuns);
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

        private static void DijkstraRunTime(Dijkstra dijkstra, Node origin, Node destination)
        {
            Stopwatch stopWatch = new Stopwatch();

            long nanosecondsPerTick = (1000L*1000L*1000L) / Stopwatch.Frequency;

            stopWatch.Start();
            dijkstra.GetRoute(origin.OsmID, destination.OsmID);
            stopWatch.Stop();

            logger.Info("Dijkstra execution time: {0:0.000} (ms)", stopWatch.ElapsedTicks * nanosecondsPerTick / 1000000);
        }
    }
}