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

            var forwardQueueIsEmpty = false;
            var backwardQueueIsEmpty = false;

            double mu = double.PositiveInfinity;

            if(!dijkstraStepsForwardQueue.TryPeek(out DijkstraStep? bestForwardStep, out double bestForwardPriority))
            {
                throw new Exception("Error retrieving initial Forward Dijkstra Step.");
            }
            if(!dijkstraStepsBackwardQueue.TryPeek(out DijkstraStep? bestBackwardStep, out double bestBackwardPriority))
            {
                throw new Exception("Error retrieving initial Backward Dijkstra Step.");
            }

            while(!forwardQueueIsEmpty || !backwardQueueIsEmpty)
            {
                // Forward queue
                if(dijkstraStepsForwardQueue.TryDequeue(out DijkstraStep? currentForwardStep, out double forwardPriority))
                {
                    var activeForwardNode = currentForwardStep.ActiveNode!;
                    if(forwardPriority <= bestForwardSteps[activeForwardNode!.Idx].CumulatedCost)
                    {
                        foreach(var outwardEdge in activeForwardNode.OutwardEdges)
                        {
                            AddForwardStep(currentForwardStep, outwardEdge.TargetNode, currentForwardStep!.CumulatedCost + outwardEdge.Cost);
                            if(bestBackwardSteps.ContainsKey(outwardEdge.TargetNode.Idx) && (forwardPriority + outwardEdge.Cost + bestBackwardSteps[outwardEdge.TargetNode.Idx].CumulatedCost) < mu)
                            {
                                bestBackwardStep = bestBackwardSteps[outwardEdge.TargetNode.Idx];
                                mu = forwardPriority + outwardEdge.Cost + bestBackwardStep.CumulatedCost;
                                bestForwardStep = currentForwardStep;
                            }
                        }
                    }
                }
                else
                {
                    forwardQueueIsEmpty = true;
                }

                // Backward queue
                if(dijkstraStepsBackwardQueue.TryDequeue(out DijkstraStep? currentBackwardStep, out double backwardPriority))
                {
                    var activeBackwardNode = currentBackwardStep.ActiveNode!;
                    if(backwardPriority <= bestBackwardSteps[activeBackwardNode!.Idx].CumulatedCost)
                    {
                        foreach(var inwardEdge in activeBackwardNode.InwardEdges)
                        {
                            AddBackwardStep(currentBackwardStep, inwardEdge.SourceNode, currentBackwardStep!.CumulatedCost + inwardEdge.Cost);
                            if(bestForwardSteps.ContainsKey(inwardEdge.SourceNode.Idx) && (backwardPriority + inwardEdge.Cost + bestForwardSteps[inwardEdge.SourceNode.Idx].CumulatedCost) < mu)
                            {
                                bestForwardStep = bestForwardSteps[inwardEdge.SourceNode.Idx];
                                mu = backwardPriority + inwardEdge.Cost + bestForwardStep.CumulatedCost;
                                bestBackwardStep = currentBackwardStep;
                            }
                        }
                    }
                }
                else
                {
                    backwardQueueIsEmpty = true;
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

            bestForwardSteps.Clear();
            bestBackwardSteps.Clear();

            forwardRoute.Clear();
            backwardRoute.Clear();

            return route;
        }

        private void AddForwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestForwardSteps.ContainsKey(nextNode!.Idx);
            if (!exist || bestForwardSteps[nextNode.Idx].CumulatedCost > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsForwardQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestForwardSteps.Add(nextNode.Idx, step);
                }
                else
                {
                    bestForwardSteps[nextNode.Idx] = step;
                }
            }
        }

        private void AddBackwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestBackwardSteps.ContainsKey(nextNode!.Idx);
            if (!exist || bestBackwardSteps[nextNode.Idx].CumulatedCost > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsBackwardQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestBackwardSteps.Add(nextNode.Idx, step);
                }
                else
                {
                    bestBackwardSteps[nextNode.Idx] = step;
                }
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