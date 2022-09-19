using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.DijkstraGeom
{
    public class DijkstraGeom : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static DijkstraInstance[] history;

        public override void Initialize(Graph graph)
        {
            if (_graph != graph)
            {
                base.Initialize(graph);
                history = new DijkstraInstance[graph.GetNodeCount()];
            }
        }

        public void TraceRoute()
        {
            logger.Debug("Display route Nodes:");
            foreach (Node node in route)
            {
                logger.Debug("Node OsmId = {0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode)
        {
            try
            {
                route.Clear();

                var instance = GenerateInstance(originNode, destinationNode);

                ReconstructRoute(instance, destinationNode.Idx);
                routeCost = instance.TotalCosts[destinationNode.Idx];

                return route;
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Impossible to route");
                throw;
            }
        }

        private DijkstraInstance GenerateInstance(Node originNode, Node destinationNode)
        {
            DijkstraInstance instance;
            var size = _graph.GetNodeCount();
            var quickRoute = false;
            var threshold = 100;

            if (history[originNode.Idx] == null)
            {
                instance = new DijkstraInstance() { Idx = originNode.Idx, Origins = new int[size], TotalCosts = new double[size], Depths = new int[size] };
                Array.Fill(instance.TotalCosts, Double.MaxValue);
                instance.Origins[originNode.Idx] = -1;
                instance.TotalCosts[originNode.Idx] = 0;

                var queue = new PriorityQueue<int, double>();
                queue.Enqueue(originNode.Idx, 0);

                var maxCost = double.MaxValue;

                while (queue.TryDequeue(out int currentIdx, out double priority))
                {
                    if ((quickRoute || instance.Depths[currentIdx] < threshold*2) && currentIdx == destinationNode.Idx)
                    {
                        if(!quickRoute)
                        {
                            logger.Info("Almost quick!");
                        }
                        //We only interrupt Dijkstra when quickrouting, and not when storing a full dijkstra.
                        break;
                    }

                    if (priority <= instance.TotalCosts[currentIdx] && (!quickRoute || priority <= maxCost))
                    {
                        var activeNode = _graph.GetNodeByIndex(currentIdx);
                        foreach (var outwardEdge in activeNode.OutwardEdges)
                        {
                            var target = outwardEdge.TargetNode.Idx;
                            var newCost = instance.TotalCosts[currentIdx] + outwardEdge.Cost;

                            if (newCost < instance.TotalCosts[target] && (!quickRoute || newCost <= maxCost))
                            {
                                if (history[target] != null)
                                {
                                    maxCost = Math.Min(maxCost, history[target].TotalCosts[destinationNode.Idx] + newCost);
                                    if (instance.Depths[currentIdx] < threshold)
                                    {
                                        if (!quickRoute)
                                        {
                                            quickRoute = true;
                                            logger.Info("Quick!");
                                        }
                                    }
                                }

                                instance.TotalCosts[target] = newCost;
                                instance.Depths[target] = instance.Depths[currentIdx] + 1;
                                instance.Origins[target] = currentIdx;

                                if (!quickRoute)
                                {
                                    queue.Enqueue(target, newCost);
                                }
                                else
                                {
                                    var distance = Helper.GetDistance(outwardEdge.TargetNode, destinationNode);
                                    var heuristic = newCost + distance * _graph.MinCostPerDistance;
                                    if (heuristic <= maxCost)
                                    {
                                        queue.Enqueue(target, newCost);
                                    }
                                }
                            }

                            if (!quickRoute)
                            {
                                history[originNode.Idx] = instance;
                            }
                        }
                    }
                }
            }
            else
            {
                instance = history[originNode.Idx];
            }

            return instance;
        }

        private void ReconstructRoute(DijkstraInstance instance, int currentNode, HashSet<int> bag = null)
        {
            if(bag == null)
            {
                bag = new HashSet<int>();
            }
            if (currentNode >= 0 && !bag.Contains(currentNode))
            {
                bag.Add(currentNode);
                ReconstructRoute(instance, instance.Origins[currentNode], bag);
                route.Add(_graph.GetNodeByIndex(currentNode));
            }
        }
    }
}