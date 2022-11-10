using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.ContractionDijkstra
{
    public class ShortcutEdge : Edge
    {
        public DijkstraStep Steps;
    }

    public class ContractionDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();
        private ShortcutEdge[][] outgoingShortcuts;
        private ShortcutEdge[][] incomingShortcuts;


        public void TraceRoute()
        {
            logger.Debug("Displaying {0} route Nodes (OsmId):", route.Count);
            foreach(Node node in route)
            {
                logger.Debug("{0}", node.OsmID);
            }
        }

        public override void Initialize(Graph graph)
        {
            base.Initialize(graph);
            ContractGraph();
        }

        private void ContractGraph()
        {
            logger.Info("Starting contraction");
            var nodes = _graph.GetNodes();
            outgoingShortcuts = new ShortcutEdge[nodes.Length][];
            incomingShortcuts = new ShortcutEdge[nodes.Length][];
            var shortcuts = new List<ShortcutEdge>();

            foreach (StepDirection direction in Enum.GetValues(typeof(StepDirection)))
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    var smallerThanI = 1;
                    AddStep(null, nodes[i], 0);


                    while (dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority) && smallerThanI > 0)
                    {
                        var activeNode = currentStep!.ActiveNode;
                        if (activeNode.Idx <= i)
                            smallerThanI--;
                        if (priority <= bestScoreForNode[activeNode!.Idx])
                        {
                            if (activeNode.Idx > i && currentStep.PreviousStep != null)
                            {
                                //Create shortcut
                                shortcuts.Add(
                                    new ShortcutEdge()
                                    {
                                        Cost = currentStep.CumulatedCost,
                                        SourceNode = direction == StepDirection.Forward ? nodes[i] : activeNode,
                                        TargetNode = direction == StepDirection.Forward ? activeNode : nodes[i],
                                        Steps = currentStep
                                    }
                                );
                            }

                            if (direction == StepDirection.Forward)
                            {
                                IEnumerable<Edge> edgesToFollow = activeNode.OutwardEdges;
                                if (outgoingShortcuts[activeNode.Idx] != null)
                                {
                                    edgesToFollow = edgesToFollow.Union(outgoingShortcuts[activeNode.Idx]);
                                }
                                foreach (var edge in edgesToFollow)
                                {
                                    if (edge.TargetNode.Idx > activeNode.Idx || activeNode.Idx == i)
                                    {
                                        if (AddStep(currentStep, edge.TargetNode, currentStep.CumulatedCost + edge.Cost) && edge.TargetNode.Idx < i)
                                            smallerThanI++;
                                    }
                                }
                            }
                            else
                            {
                                IEnumerable<Edge> edgesToFollow = activeNode.InwardEdges;
                                if (incomingShortcuts[activeNode.Idx] != null)
                                {
                                    edgesToFollow = edgesToFollow.Union(incomingShortcuts[activeNode.Idx]);
                                }
                                foreach (var edge in edgesToFollow)
                                {
                                    if (edge.SourceNode.Idx > activeNode.Idx || activeNode.Idx == i)
                                    {
                                        if (AddStep(currentStep, edge.SourceNode, currentStep.CumulatedCost + edge.Cost) && edge.SourceNode.Idx < i)
                                            smallerThanI++;
                                    }
                                }
                            }
                        }
                    }

                    if (direction == StepDirection.Forward)
                    {
                        outgoingShortcuts[i] = shortcuts.Count > 0 ? shortcuts.ToArray() : null;
                    }
                    else
                    {
                        incomingShortcuts[i] = shortcuts.Count > 0 ? shortcuts.ToArray() : null;
                    }
                    shortcuts.Clear();
                    dijkstraStepsQueue.Clear();
                    bestScoreForNode.Clear();
                }
            }

            logger.Info("Contraction done");
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            route.Clear();
            routeCost = 0;

            AddStep(null, originNode, 0);

            while(dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority))
            {
                var activeNode = currentStep!.ActiveNode;
                if(activeNode == destinationNode)
                {
                    ReconstructRoute(currentStep);
                    routeCost = currentStep.CumulatedCost;
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

        private bool AddStep(DijkstraStep? previousStep, Node? nextNode, double cumulatedCost)
        {
            var exist = bestScoreForNode.ContainsKey(nextNode!.Idx);
            if (!exist || bestScoreForNode[nextNode.Idx] > cumulatedCost)
            {
                var step = new DijkstraStep { PreviousStep = previousStep, ActiveNode = nextNode, CumulatedCost = cumulatedCost};
                dijkstraStepsQueue.Enqueue(step, cumulatedCost);

                if(!exist)
                {
                    bestScoreForNode.Add(nextNode.Idx, cumulatedCost);
                    return true;
                }
                else
                {
                    bestScoreForNode[nextNode.Idx] = cumulatedCost;
                    return false;
                }
            }
            return false;
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