using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.Dijkstra
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


        private List<DijkstraStep> forwardSteps = new List<DijkstraStep>(0);
        private List<DijkstraStep> backwardSteps = new List<DijkstraStep>(0);


        private List<int> visitedForwardNodesIdxs = new List<int>(0);
        private List<int> visitedBackwardNodesIdxs = new List<int>(0);


   
        public BidirectionalDijkstra(Graph graph) : base(graph) { }

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

            AddForwardStep(null, originNode, 0);
            AddBackwardStep(null, destinationNode, 0);

            var commonNodeFound = false;

            while(!commonNodeFound)
            {
                // Forward queue
                if(dijkstraStepsForwardQueue.TryDequeue(out DijkstraStep? currentForwardStep, out double forwardPriority))
                {
                    var activeNode = currentForwardStep!.ActiveNode;

                    // logger.Debug("Analizing forward Node {0}", activeNode!.Idx);

                    commonNodeFound = visitedBackwardNodesIdxs.Contains(activeNode!.Idx);

                    if(commonNodeFound || activeNode == destinationNode)
                    {
                        if(commonNodeFound)
                            logger.Debug("Common Node OsmId: {0} (f)", activeNode.OsmID);
                        if(activeNode == originNode)
                            logger.Debug("Active Node OsmId: {0} (f) == destination Node", activeNode.OsmID);

                        //PrintStepNodes();
                        ReconstructForwardRoute(currentForwardStep);
                        var backwardStepMatch = backwardSteps.Find(s => s.ActiveNode!.Idx == activeNode.Idx);
                        var backwardPreviousStep = backwardStepMatch!.PreviousStep;
                        // ReconstructBackwardRoute(backwardPreviousStep);
                        ReconstructBackwardRoute(backwardStepMatch);

                        break;
                    }

                    if(forwardPriority <= bestScoreForForwardNode[activeNode!.Idx])
                    {
                        foreach(var outwardEdge in activeNode.OutwardEdges)
                        {
                            AddForwardStep(currentForwardStep, outwardEdge.TargetNode, currentForwardStep.CumulatedCost + outwardEdge.Cost);
                        }
                    }
                    visitedForwardNodesIdxs.Add(activeNode.Idx);
                }

                // Backward queue
                if(dijkstraStepsBackwardQueue.TryDequeue(out DijkstraStep? currentBackwardStep, out double backwardPriority))
                {
                    var activeNode = currentBackwardStep!.ActiveNode;

                    // logger.Debug("Analizing backward Node {0}", activeNode!.Idx);

                    commonNodeFound = visitedForwardNodesIdxs.Contains(activeNode!.Idx);
                    
                    if(commonNodeFound || activeNode == originNode)
                    {
                        if(commonNodeFound)
                            logger.Debug("Common Node OsmId: {0} (b)", activeNode.OsmID);
                        if(activeNode == originNode)
                            logger.Debug("Active Node OsmId: {0} (b) == origin Node", activeNode.OsmID);

                        ReconstructBackwardRoute(currentBackwardStep);
                        break;
                    }

                    if(backwardPriority <= bestScoreForBackwardNode[activeNode!.Idx])
                    {
                        foreach(var inwardEdge in activeNode.InwardEdges)
                        {
                            AddBackwardStep(currentBackwardStep, inwardEdge.SourceNode, currentBackwardStep.CumulatedCost + inwardEdge.Cost);
                        }
                    }
                    visitedBackwardNodesIdxs.Add(activeNode.Idx);
                }

                

                
            }

            dijkstraStepsForwardQueue.Clear();
            bestScoreForForwardNode.Clear();

            route = forwardRoute.Concat(backwardRoute).ToList();

            // PrintStepNodes();

            return route;
        }

        private void AddForwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForForwardNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForForwardNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsForwardQueue.Enqueue(step, cumulatedCost);

                forwardSteps.Add(step);

                if(!exist)
                {
                    bestScoreForForwardNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForForwardNode[nextNode.Idx] = cumulatedCost;
                }
            }
        }

        private void AddBackwardStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForBackwardNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForBackwardNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost };
                dijkstraStepsBackwardQueue.Enqueue(step, cumulatedCost);

                backwardSteps.Add(step);

                if(!exist)
                {
                    bestScoreForBackwardNode.Add(nextNode.Idx, cumulatedCost);
                }
                else
                {
                    bestScoreForBackwardNode[nextNode.Idx] = cumulatedCost;
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




        private void PrintStepNodes()
        {
            logger.Debug("forward steps:");
            foreach(var step in forwardSteps)
            {
                logger.Debug("active node idx {0}", step.ActiveNode!.Idx);
            }
            
            logger.Debug("backward steps:");
            foreach(var step in backwardSteps)
            {
                logger.Debug("active node idx {0}", step.ActiveNode!.Idx);
            }
        }
    }
}