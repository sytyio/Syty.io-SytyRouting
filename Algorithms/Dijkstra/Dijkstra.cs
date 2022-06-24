using System.Diagnostics;
using NLog;

namespace SytyRouting.Algorithms.Dijkstra
{
    public class Dijkstra
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Graph _graph;
        private List<Node> route = new List<Node>();
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        public Dijkstra(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            Node originNode;
            try
            {
                originNode = _graph.GetNodeByOsmId(originNodeOsmId);
            }
            catch (ArgumentException e)
            {
                logger.Info("Origin node (source_osm = {0}) not found", originNodeOsmId);
                logger.Info("{0}: {1}", e.GetType().Name, e.Message);

                throw new Exception("Unknown value for node osm id.");
            }

            Node destinationNode;
            try
            {
                destinationNode = _graph.GetNodeByOsmId(destinationNodeOsmId);
            }
            catch (ArgumentException e)
            {
                logger.Info("Destination node (source_osm = {0}) not found", destinationNodeOsmId);
                logger.Info("{0}: {1}", e.GetType().Name, e.Message);

                throw new Exception("Unknown value for node osm id.");
            }

            return GetRoute(originNode, destinationNode);
        }

        private void AddStep(Node nextNode, double cumulatedCost)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsQueue.Enqueue(step, cumulatedCost);
                if(!exist)
                {
                    bestScoreForNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForNode[nextNode.Idx] = cumulatedCost;
                }
            }
        }

        public List<Node> GetRoute(Node originNode, Node destinationNode)
        {
            route.Clear();
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            logger.Info("Origin Node     \t OsmId = {0}", originNode?.OsmID);
            logger.Info("Destination Node\t OsmId = {0}", destinationNode?.OsmID);

            AddStep(originNode, 0);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;
                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep);
                    break;
                }
                if(priority <= bestScoreForNode[activeNode.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        AddStep(outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost);
                    }
                }
            }
            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route created in {0} (HH:MM:S.mS)", totalTime);

            return route;
        }

        private void ReconstructRoute(DijkstraStep currentStep)
        {
            if (currentStep != null)
            {
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode);
                logger.Info("Node OsmId = {0}", currentStep.ActiveNode.OsmID);
            }
        }
    }
}