using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.HeuristicDijkstra
{
    public class HeuristicDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        public void TraceRoute()
        {
            logger.Debug("Displaying {0} route Nodes (OsmId):", route.Count);
            foreach(Node node in route)
            {
                logger.Debug("{0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            route.Clear();
            routeCost = 0;

            AddStep(null, originNode, 0, destinationNode);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double heuristic))
            {
                var activeNode = currentStep!.ActiveNode;
                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep);
                    routeCost = currentStep.CumulatedCost;
                    break;
                }
                if(currentStep.CumulatedCost <= bestScoreForNode[activeNode!.Idx])
                {
                    foreach(var outwardEdge in activeNode.OutwardEdges)
                    {
                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, destinationNode);
                    }
                }
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, Node destinationNode)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var distance = Helper.GetDistance(nextNode, destinationNode);
                var heuristic = cumulatedCost +  distance * _graph.MinCostPerDistance;
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, Heuristic = heuristic };
                if(previousStep != null && previousStep.Heuristic > heuristic)
                {
                    throw new Exception("Impossible case found.");
                }
                if (!bestScoreForNode.ContainsKey(destinationNode.Idx) || heuristic <= bestScoreForNode[destinationNode.Idx])
                {
                    dijkstraStepsQueue.Enqueue(step, heuristic);

                    if (!exist)
                    {
                        bestScoreForNode.Add(nextNode.Idx, cumulatedCost);
                    }
                    else
                    {
                        bestScoreForNode[nextNode.Idx] = cumulatedCost;
                    }
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