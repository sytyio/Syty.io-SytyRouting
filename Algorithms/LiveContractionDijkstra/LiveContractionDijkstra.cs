using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.LiveContractionDijkstra
{
    public class ContractionInfo
    {
        public int Level = 0;
        public List<ContractedEdge> PassingEdges = new List<ContractedEdge>();
        public List<ContractedEdge> OutgoingContractedEdges = new List<ContractedEdge>();
        public bool Explored = false;
    }

    public class ContractedEdge
    {
        public List<Edge> SubEdges = new List<Edge>();
        public double ContractedCost;
    }

    public class ExploreStep
    {
        public Edge Edge;

        public Edge PreviousEdge;
        public double CostIncludingEdge;
        public int MinLevel;
    }

    public class LiveContractionDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        private PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
        private Dictionary<int, double> bestScoreForNode = new Dictionary<int, double>();

        private ContractionInfo[] nodeInfo  = new ContractionInfo[0];

        private Random rand = new Random((int)(DateTime.Now.Ticks%1234567));

        public override void Initialize(Graph graph)
        {
            base.Initialize(graph);
            nodeInfo = new ContractionInfo[graph.GetNodeCount()];
            for (int i = 0; i < nodeInfo.Length; i++)
            {
                nodeInfo[i] = new ContractionInfo();
            }
        }

        public void TraceRoute()
        {
            logger.Debug("Displaying {0} route Nodes (OsmId):", route.Count);
            foreach(Node node in route)
            {
                logger.Debug("{0}", node.OsmID);
            }
        }

        private int GetLevel()
        {
            var level = 1;
            while(rand.NextDouble() > 0.5)
            {
                level += 1;
            }
            return level;
        }

        private void BuildContraction(Node node)
        {
            if(!nodeInfo[node.Idx].Explored)
            {
                nodeInfo[node.Idx].Level = GetLevel();
                nodeInfo[node.Idx].Explored = true;
                bestScoreForNode.Clear();

                var exploreQueue = new PriorityQueue<ExploreStep, double>();
                foreach(var edge in node.OutwardEdges)
                {
                    exploreQueue.Enqueue(new ExploreStep() { Edge = edge, CostIncludingEdge = edge.Cost, MinLevel = 0 }, edge.Cost);
                }

                while(exploreQueue.TryDequeue(out ExploreStep currentStep, out double priority))
                {

                }
            }
        }

       

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            route.Clear();
            routeCost = 0;

            BuildContraction(originNode);
            BuildContraction(destinationNode);

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