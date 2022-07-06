using NLog;
using SytyRouting.Algorithms.Dijkstra;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.BackwardDijkstra
{
    public class BackwardDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();
   
        public BackwardDijkstra(Graph graph) : base(graph) { }

        public void TraceRoute()
        {
            logger.Debug("Displaying {0} route Nodes:", route.Count);
            foreach(Node node in route)
            {
                logger.Debug("Node OsmId = {0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            route.Clear();

            AddStep(null, destinationNode, 0);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;
                if(activeNode == originNode)
                {
                    ReconstructRoute(currentStep);
                    logger.Trace("                      <=            Backward cost {0}", currentStep.CumulatedCost);
                    break;
                }
                if(priority <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var inwardEdge in activeNode.InwardEdges)
                    {
                        AddStep(currentStep, inwardEdge.SourceNode, currentStep.CumulatedCost + inwardEdge.Cost);
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
                route.Add(currentStep.ActiveNode!);
                ReconstructRoute(currentStep.PreviousStep);
            }
        }
    }
}