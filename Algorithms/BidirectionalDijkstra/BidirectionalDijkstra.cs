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

        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();

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

            double mu = double.PositiveInfinity;

            double forwardPriority = 0;
            var bestForwardStep = new DijkstraStep {PreviousStep = null, ActiveNode = originNode, CumulatedCost = forwardPriority, Direction = StepDirection.Forward};
            AddStep(null, originNode, forwardPriority, StepDirection.Forward);

            double backwardPriority = 0;
            var bestBackwardStep = new DijkstraStep {PreviousStep = null, ActiveNode = destinationNode, CumulatedCost = backwardPriority, Direction = StepDirection.Backward};
            AddStep(null, destinationNode, backwardPriority, StepDirection.Backward);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep.ActiveNode!;   

                if(currentStep.Direction == StepDirection.Forward)
                {
                    if(priority <= bestForwardSteps[activeNode!.Idx].CumulatedCost)
                    {
                        foreach(var outwardEdge in activeNode.OutwardEdges)
                        {
                            AddStep(currentStep, outwardEdge.TargetNode, currentStep!.CumulatedCost + outwardEdge.Cost, StepDirection.Forward);
                            if(bestBackwardSteps.ContainsKey(outwardEdge.TargetNode.Idx) && (priority + outwardEdge.Cost + bestBackwardSteps[outwardEdge.TargetNode.Idx].CumulatedCost) < mu)
                            {
                                bestBackwardStep = bestBackwardSteps[outwardEdge.TargetNode.Idx];
                                mu = priority + outwardEdge.Cost + bestBackwardStep.CumulatedCost;
                                bestForwardStep = currentStep;
                            }
                        }
                    }
                    forwardPriority = priority;
                }
                else
                {
                    if(priority <= bestBackwardSteps[activeNode!.Idx].CumulatedCost)
                    {
                        foreach(var inwardEdge in activeNode.InwardEdges)
                        {
                            AddStep(currentStep, inwardEdge.SourceNode, currentStep!.CumulatedCost + inwardEdge.Cost, StepDirection.Backward);
                            if(bestForwardSteps.ContainsKey(inwardEdge.SourceNode.Idx) && (priority + inwardEdge.Cost + bestForwardSteps[inwardEdge.SourceNode.Idx].CumulatedCost) < mu)
                            {
                                bestForwardStep = bestForwardSteps[inwardEdge.SourceNode.Idx];
                                mu = priority + inwardEdge.Cost + bestForwardStep.CumulatedCost;
                                bestBackwardStep = currentStep;
                            }
                        }
                    }
                    backwardPriority = priority;
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

            dijkstraStepsQueue.Clear();

            bestForwardSteps.Clear();
            bestBackwardSteps.Clear();

            forwardRoute.Clear();
            backwardRoute.Clear();

            return route;
        }

        private void AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost, StepDirection direction)
        {
            if(direction == StepDirection.Forward)
            {
                var exist = bestForwardSteps.ContainsKey(nextNode!.Idx);
                if (!exist || bestForwardSteps[nextNode.Idx].CumulatedCost > cumulatedCost)
                {
                    var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, Direction = direction };
                    dijkstraStepsQueue.Enqueue(step, cumulatedCost);

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
            else
            {
                var exist = bestBackwardSteps.ContainsKey(nextNode!.Idx);
                if (!exist || bestBackwardSteps[nextNode.Idx].CumulatedCost > cumulatedCost)
                {
                    var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost, Direction = direction };
                    dijkstraStepsQueue.Enqueue(step, cumulatedCost);

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