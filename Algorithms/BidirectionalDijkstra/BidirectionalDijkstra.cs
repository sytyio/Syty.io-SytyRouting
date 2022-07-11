using NLog;
using SytyRouting.Algorithms.Dijkstra;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.BidirectionalDijkstra
{
    public class BidirectionalDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private List<Node> forwardRoute = new List<Node>();
        private List<Node> backwardRoute = new List<Node>();

        private PriorityQueue<DijkstraStep, double> dijkstraStepsForwardQueue = new PriorityQueue<DijkstraStep, double>();
        private PriorityQueue<DijkstraStep, double> dijkstraStepsBackwardQueue = new PriorityQueue<DijkstraStep, double>();

        private Dictionary<int, double> bestScoreForForwardNode = new Dictionary<int, double>();
        private Dictionary<int, double> bestScoreForBackwardNode = new Dictionary<int, double>();

        private Dictionary<int, DijkstraStep> bestForwardSteps  = new Dictionary<int, DijkstraStep>();
        private Dictionary<int, DijkstraStep> bestBackwardSteps = new Dictionary<int, DijkstraStep>();


   

        public void TraceRoute()
        {
            logger.Debug("Display route Nodes:");
            foreach(Node node in route)
            {
                logger.Debug("Node OsmId = {0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            route.Clear();
            routeCost = 0;

            AddForwardStep(null, originNode, 0);
            AddBackwardStep(null, destinationNode, 0);

            var emptyQueues = 0;

            double mu = double.PositiveInfinity;

            if(!dijkstraStepsForwardQueue.TryPeek(out DijkstraStep? bestForwardStep, out double bestForwardPriority))
            {
                throw new Exception("Error retrieving initial Forward Dijkstra Step.");
            }
            if(!dijkstraStepsBackwardQueue.TryPeek(out DijkstraStep? bestBackwardStep, out double bestBackwardPriority))
            {
                throw new Exception("Error retrieving initial Backward Dijkstra Step.");
            }

            while(emptyQueues < 2)
            {
                // Forward queue
                if(dijkstraStepsForwardQueue.TryDequeue(out DijkstraStep? currentForwardStep, out double forwardPriority))
                {
                    var activeForwardNode = currentForwardStep.ActiveNode!;
                    if(forwardPriority <= bestScoreForForwardNode[activeForwardNode!.Idx])
                    {
                        foreach(var outwardEdge in activeForwardNode.OutwardEdges)
                        {
                            AddForwardStep(currentForwardStep, outwardEdge.TargetNode, currentForwardStep!.CumulatedCost + outwardEdge.Cost);
                            if(bestScoreForBackwardNode.ContainsKey(outwardEdge.TargetNode.Idx) && (forwardPriority + outwardEdge.Cost + bestScoreForBackwardNode[outwardEdge.TargetNode.Idx]) < mu)
                            {
                                mu = forwardPriority + outwardEdge.Cost + bestScoreForBackwardNode[outwardEdge.TargetNode.Idx];
                                bestForwardStep = currentForwardStep;
                                
                                bestBackwardStep = bestBackwardSteps[outwardEdge.TargetNode.Idx];
                            }
                        }
                    }
                }
                else
                {
                    emptyQueues = emptyQueues + 1;
                }

                // Backward queue
                if(dijkstraStepsBackwardQueue.TryDequeue(out DijkstraStep? currentBackwardStep, out double backwardPriority))
                {
                    var activeBackwardNode = currentBackwardStep.ActiveNode!;
                    if(backwardPriority <= bestScoreForBackwardNode[activeBackwardNode!.Idx])
                    {
                        foreach(var inwardEdge in activeBackwardNode.InwardEdges)
                        {
                            AddBackwardStep(currentBackwardStep, inwardEdge.SourceNode, currentBackwardStep!.CumulatedCost + inwardEdge.Cost);
                            if(bestScoreForForwardNode.ContainsKey(inwardEdge.SourceNode.Idx) && (backwardPriority + inwardEdge.Cost + bestScoreForForwardNode[inwardEdge.SourceNode.Idx]) < mu)
                            {
                                mu = backwardPriority + inwardEdge.Cost + bestScoreForForwardNode[inwardEdge.SourceNode.Idx];
                                bestBackwardStep = currentBackwardStep;
                                
                                bestForwardStep = bestForwardSteps[inwardEdge.SourceNode.Idx];
                            }
                        }
                    }
                }
                else
                {
                    emptyQueues = emptyQueues + 1;
                }

                if(forwardPriority + backwardPriority >= mu)
                {
                    ReconstructForwardRoute(bestForwardStep);
                    ReconstructBackwardRoute(bestBackwardStep);
                    routeCost = mu;

                    break;
                }
                    
                    
            }

            route = forwardRoute.Concat(backwardRoute).ToList();

            dijkstraStepsForwardQueue.Clear();
            dijkstraStepsBackwardQueue.Clear();

            bestScoreForForwardNode.Clear();
            bestScoreForBackwardNode.Clear();

            bestForwardSteps.Clear();
            bestBackwardSteps.Clear();

            forwardRoute.Clear();
            backwardRoute.Clear();

            return route;
        }

        private void AddForwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForForwardNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForForwardNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsForwardQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestScoreForForwardNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForForwardNode[nextNode.Idx] = cumulatedCost;
                }
                if(bestForwardSteps.ContainsKey(nextNode.Idx))
                        bestForwardSteps[nextNode.Idx] = step;
                else
                        bestForwardSteps.Add(nextNode.Idx, step);
            }
        }

        private void AddBackwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForBackwardNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForBackwardNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsBackwardQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestScoreForBackwardNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForBackwardNode[nextNode.Idx] = cumulatedCost;
                }
                if( bestBackwardSteps.ContainsKey(nextNode.Idx))
                    bestBackwardSteps[nextNode.Idx] = step;
                else
                    bestBackwardSteps.Add(nextNode.Idx, step);
            }
        }

        private void ReconstructForwardRoute(DijkstraStep? currentStep)
        {
            if (currentStep != null)
            {
                ReconstructForwardRoute(currentStep.PreviousStep);
                forwardRoute.Add(currentStep.ActiveNode!);
            }
        }

        private void ReconstructBackwardRoute(DijkstraStep? currentStep)
        {
            if (currentStep != null)
            {
                backwardRoute.Add(currentStep.ActiveNode!);
                ReconstructBackwardRoute(currentStep.PreviousStep);
            }
        }
    }
}