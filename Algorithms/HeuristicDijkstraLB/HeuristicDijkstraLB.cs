using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.HeuristicDijkstraLB
{
    public class HeuristicDijkstraLB : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        public override void Initialize(Graph graph)
        {
            base.Initialize(graph);
        }

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
            var upperBound = double.MaxValue;

            AddStep(null, originNode, 0, destinationNode, ref upperBound);

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
                        AddStep(currentStep, outwardEdge.TargetNode, currentStep.CumulatedCost + outwardEdge.Cost, destinationNode, ref upperBound);
                    }
                }
            }

            dijkstraStepsQueue.Clear();
            bestScoreForNode.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, Node destinationNode, ref double upperBound)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx); 
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var distance = Helper.GetDistance(nextNode, destinationNode);
                var heuristic = Math.Max(cumulatedCost + distance * _graph.MinCostPerDistance, previousStep != null ? previousStep.LowerBoundVia:0);
                if (upperBound >= heuristic)
                {
                    var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, LowerBoundVia = heuristic};
                    dijkstraStepsQueue.Enqueue(step, heuristic);

                    if (!exist)
                    {
                        bestScoreForNode.Add(nextNode.Idx, cumulatedCost);
                    }
                    else
                    {
                        bestScoreForNode[nextNode.Idx] = cumulatedCost;
                    }

                    if(nextNode.Idx == destinationNode.Idx)
                    {
                        upperBound = Math.Min(upperBound, cumulatedCost);
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