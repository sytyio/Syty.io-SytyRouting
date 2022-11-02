using NLog;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.MultiDijkstra
{
    public class MultiDijkstra : BaseRoutingAlgorithm
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static DijkstraInstance[] history;
        private static List<DijkstraInstance> historyL;

        public override void Initialize(Graph graph)
        {
            base.Initialize(graph);
            history = new DijkstraInstance[graph.GetNodeCount()];
            historyL = new List<DijkstraInstance>();
        }

        public void TraceRoute()
        {
            logger.Debug("Display route Nodes:");
            foreach(Node node in route)
            {
                logger.Debug("Node OsmId = {0}", node.OsmID);
            }
        }

        protected override List<Node> RouteSearch(Node originNode, Node destinationNode, byte transportMode)
        {
            try
            {
                route.Clear();

                var instance = GenerateInstance(originNode, destinationNode, true);

                ReconstructRoute(instance, destinationNode.Idx);
                routeCost = instance.TotalCosts[destinationNode.Idx];

                return route;
            }
            catch(Exception e)
            {
                logger.Fatal(e, "Impossible to route");
                throw;
            }
        }

        private DijkstraInstance GenerateInstance(Node originNode, Node destinationNode, bool quickRoute)
        {
            DijkstraInstance instance; 
            var size = _graph.GetNodeCount();

            if (history[originNode.Idx] == null)
            {
                instance = new DijkstraInstance() { Idx = originNode.Idx, Origins = new int[size], TotalCosts = new double[size], Depths = new int[size] };
                Array.Fill(instance.TotalCosts, Double.MaxValue);
                instance.Origins[originNode.Idx] = -1;
                instance.TotalCosts[originNode.Idx] = 0;

                //Must be replaced by point loc
                var closestInstances = quickRoute ? historyL.OrderBy(t => t.Depths[originNode.Idx]).Take(5).ToArray() : null;
                var samePath = new int[size];
                Array.Fill(samePath, -1);
                if(closestInstances != null && closestInstances.Count() > 0 && closestInstances[0].Depths[originNode.Idx] < 100)
                {
                    logger.Info("Found {0} helping Dijkstras", closestInstances.Count());
                    for (int i = 0; i < closestInstances.Length; i++)
                    {
                        for (var current = destinationNode.Idx; current != -1; current = closestInstances[i].Origins[current])
                        {
                            samePath[current] = i;
                        }
                    }
                    logger.Info("Tagging done");
                    //At the end of this loop, the samePath array contains -1 for all idx that are not on the root - destination path on any of the trees, and contains a number >= 0 for all idx that are on the path
                }
                else
                {
                    quickRoute = false;
                    logger.Info("Generating a full Dijkstra");
                }
                

                var done = new bool[size];
                var queue = new PriorityQueue<int, double>();
                queue.Enqueue(originNode.Idx, 0);

                while (queue.TryDequeue(out int currentIdx, out double priority))
                {
                    if(quickRoute && samePath[currentIdx] != -1 && currentIdx != destinationNode.Idx)
                    {
                        Unroll(closestInstances[samePath[currentIdx]], instance, done, destinationNode.Idx, currentIdx);
                        done[currentIdx] = true;
                        queue.Enqueue(destinationNode.Idx, instance.TotalCosts[destinationNode.Idx]);
                    }
                    if(quickRoute && currentIdx == destinationNode.Idx)
                    {
                        //We only interrupt Dijkstra when quickrouting, and not when storing a full dijkstra.
                        break;
                    }
                    if (!done[currentIdx])
                    {
                        done[currentIdx] = true;
                        
                        var activeNode = _graph.GetNodeByIndex(currentIdx);
                        foreach (var outwardEdge in activeNode.OutwardEdges)
                        {
                            var target = outwardEdge.TargetNode.Idx;
                            var newCost = instance.TotalCosts[currentIdx] + outwardEdge.Cost;

                            if (!done[target] && newCost <= instance.TotalCosts[target])
                            {
                                instance.TotalCosts[target] = newCost;
                                instance.Origins[target] = currentIdx;
                                instance.Depths[target] = instance.Depths[currentIdx] + 1;
                                if (!quickRoute || newCost <= instance.TotalCosts[destinationNode.Idx])
                                {
                                    queue.Enqueue(target, newCost);
                                }
                            }
                        }
                    }
                }


                if (!quickRoute)
                {
                    history[originNode.Idx] = instance;
                    historyL.Add(instance);
                }
            }
            else
            {
                instance = history[originNode.Idx];
            }

            return instance;
        }

        private void Unroll(DijkstraInstance source, DijkstraInstance current, bool[] done,  int target, int origin)
        {
            current.TotalCosts[target] = source.TotalCosts[target] - source.TotalCosts[origin]+ current.TotalCosts[origin];
            current.Origins[target] = source.Origins[target];
            current.Depths[target] = source.Depths[target] - source.Depths[origin]+ current.Depths[origin];
            done[target] = true;

            var next = source.Origins[target];
            if (next != origin)
            {
                Unroll(source, current, done, next, origin);
            }
        }

        private void ReconstructRoute(DijkstraInstance instance, int currentNode)
        {
            if (currentNode >= 0)
            {
                ReconstructRoute(instance, instance.Origins[currentNode]);
                route.Add(_graph.GetNodeByIndex(currentNode));
            }
        }
    }
}