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

        public List<Node> GetRoute(double x1, double y1, double x2, double y2)
        {           
            var originNode = _graph.GetNodeByLatitudeLongitude(x1, y1);
            var destinationNode = _graph.GetNodeByLatitudeLongitude(x2, y2);
            
            return GetRoute(originNode, destinationNode);
        }

        public void TraceRoute()
        {
            foreach(Node node in route)
            {
                logger.Trace("Node OsmId = {0}", node.OsmID);
            }
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
                logger.Debug("Origin node (source_osm = {0}) not found", originNodeOsmId);
                logger.Debug("{0}: {1}", e.GetType().Name, e.Message);

                throw new Exception("Unknown value for node osm id.");
            }

            Node destinationNode;
            try
            {
                destinationNode = _graph.GetNodeByOsmId(destinationNodeOsmId);
            }
            catch (ArgumentException e)
            {
                logger.Debug("Destination node (source_osm = {0}) not found", destinationNodeOsmId);
                logger.Debug("{0}: {1}", e.GetType().Name, e.Message);

                throw new Exception("Unknown value for node osm id.");
            }

            return GetRoute(originNode, destinationNode);
        }

        private List<Node> GetRoute(Node originNode, Node destinationNode)
        {
            route.Clear();

            AddStep(null, originNode, 0);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;
                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep);
                    break;
                }
                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost);
                    }
                }
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };

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

        private void ReconstructRoute(DijkstraStep? currentStep)
        {
            if (currentStep != null)
            {
                ReconstructRoute(currentStep.PreviousStep);
                route.Add(currentStep.ActiveNode!);
            }
        }
    }
}