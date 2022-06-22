using NLog;
namespace SytyRouting
{
    public class DijkstraTest
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] nodes =  new Node[0];
        private Node[] reducedSetOfNodes =  new Node[0];
        private Dictionary<int, Node> neighborhood = new Dictionary<int, Node>();


        public DijkstraTest(Node[] graphNodes, long originNodeOsmId, int maxNumberOfNodes)
        {
            nodes = graphNodes;
            reducedSetOfNodes =  new Node[maxNumberOfNodes];

            CreateReducedDataSet(originNodeOsmId, maxNumberOfNodes);
        }

        public Node[] GetNodes()
        {
            foreach (var node in reducedSetOfNodes)
            {
                logger.Debug("Node Idx={0}, OsmId={1}, X = {2}, Y = {3}",
                    node.Idx, node.OsmID, node.X, node.Y);
                GetEdges(node);
            }
            
            return reducedSetOfNodes;
        }

        private void GetEdges(Node node)
        {
            logger.Debug("\tInward Edges in Node {0}:", node.OsmID);
            foreach(var edge in node.InwardEdges)
            {
                logger.Debug("\t\tEdge: {0},\tcost: {1},\tsource Node Id: {2},\ttarget Node Id: {3};",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.TargetNode?.OsmID);
            }
            
            logger.Debug("\tOutward Edges in Node {0}:", node.OsmID);
            foreach(var edge in node.OutwardEdges)
            {
                logger.Debug("\t\tEdge: {0},\tcost: {1},\tsource Node Id: {2},\ttarget Node Id: {3};",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.TargetNode?.OsmID);
            }
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            var route = new List<Node>();

            int i = 0;

            Dictionary<int, Node> visitedNodes = new Dictionary<int, Node>();

            PriorityQueue<DijkstraStep, double> dijkstraStepsQueue = new PriorityQueue<DijkstraStep, double>();
            PriorityQueue<DijkstraStep, double> backwardStartSteps = new PriorityQueue<DijkstraStep, double>();


            var originNode = Array.Find(nodes, n => n.OsmID == originNodeOsmId);
            var destinationNode = Array.Find(nodes, n => n.OsmID == destinationNodeOsmId);
            if(originNode == null)
            {
                logger.Info("Couldn't find origin node (source_osm = {0})", originNodeOsmId);
            }
            else if(destinationNode == null)
            {
                logger.Info("Couldn't find destination node (source_osm = {0})", destinationNodeOsmId);
            }
            else
            {
                logger.Info("Origin Node     \t OsmId = {0}", originNode?.OsmID);
                logger.Info("Destination Node\t OsmId = {0}", destinationNode?.OsmID);

                var firstStep = new DijkstraStep{TargetNode = originNode, CumulatedCost = 0};
                dijkstraStepsQueue.Enqueue(firstStep, firstStep.CumulatedCost);
                
                // logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
                    // i, dijkstraSteps[i].Idx, dijkstraSteps[i].PreviousStep?.TargetNode?.OsmID, dijkstraSteps[i].TargetNode?.OsmID, dijkstraSteps[i].CumulatedCost);
                
                while(!visitedNodes.ContainsKey(destinationNode.Idx))
                {
                    dijkstraStepsQueue.TryDequeue(out DijkstraStep? currentStep, out double priority);

                    // logger.Debug("Dequeueing Step Idx = {0},\tprevious node OsmId = {1},\ttarget node OsmId = {2},\tcumulated cost = {3},\tpriority = {4}",
                    // currentStep?.Idx, currentStep?.PreviousStep?.TargetNode?.OsmID, currentStep?.TargetNode?.OsmID, currentStep?.CumulatedCost, priority);

                    var targetNode = currentStep?.TargetNode;
                    logger.Debug(":: {0} ::", i++);
                    logger.Debug("targetNode:\t\t\t\t{0}({1})", targetNode.Idx, targetNode.OsmID);
                    if(targetNode != null && !visitedNodes.ContainsKey(targetNode.Idx))
                    {
                        foreach(var outwardEdge in targetNode.OutwardEdges)
                        {
                            logger.Debug("outwardEdge.TargetNode.Idx:\t\t{0}({1})", outwardEdge.TargetNode.Idx, outwardEdge.TargetNode.OsmID);
                            

                            if(!visitedNodes.ContainsKey(outwardEdge.TargetNode.Idx))
                            {
                                var dijkstraStep = new DijkstraStep{PreviousStep = currentStep, TargetNode = outwardEdge.TargetNode, CumulatedCost = outwardEdge.Cost + currentStep.CumulatedCost};
                                dijkstraStepsQueue.Enqueue(dijkstraStep, dijkstraStep.CumulatedCost);
                                
                                if(dijkstraStep.TargetNode.OsmID == destinationNodeOsmId)
                                {
                                    backwardStartSteps.Enqueue(dijkstraStep, dijkstraStep.CumulatedCost);
                                }

                                // logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
                                    // i, dijkstraSteps[i].Idx, dijkstraSteps[i].PreviousStep?.TargetNode?.OsmID, dijkstraSteps[i].TargetNode?.OsmID, dijkstraSteps[i].CumulatedCost);
                                
                            }
                            else
                            {
                                logger.Debug("Node: {0}({1}) has already been visited", outwardEdge.TargetNode.Idx, outwardEdge.TargetNode.OsmID);
                            }
                        }
                        visitedNodes.Add(targetNode.Idx, targetNode);
                        logger.Debug("visitedNodes:");
                        foreach(var node in visitedNodes)
                        {
                            logger.Debug("\t\t\t\t: Key:  {0}({2}), Idx:{1}", node.Key, node.Value.Idx, node.Value.OsmID);
                        }
                        logger.Debug("::   ::");
                        // Console.Read();
                    }
                }
                logger.Debug("Destination node reached");
            }

            logger.Debug("Visited nodes:");
            foreach(var visitedNode in visitedNodes)
            {
                logger.Debug("Visited node OsmId = {0}", visitedNode.Value.OsmID);
            }

            // logger.Debug("Dijkstra steps:");
            // foreach(var step in dijkstraSteps)
            // {
            //     logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
            //         step.Key, step.Value.Idx, step.Value.PreviousStep?.TargetNode?.OsmID, step.Value.TargetNode?.OsmID, step.Value.CumulatedCost);
            // }

            // logger.Debug("Dijkstra steps queue:");
            // while (dijkstraStepsQueue.TryDequeue(out DijkstraStep? step, out double priority))
            // {
            //     logger.Debug("Step Idx = {0},\tprevious node OsmId = {1},\ttarget node OsmId = {2},\tcumulated cost = {3},\tpriority = {4}",
            //         step?.Idx, step?.PreviousStep?.TargetNode?.OsmID, step?.TargetNode?.OsmID, step?.CumulatedCost, priority);
            // }
            
            backwardStartSteps.TryPeek(out DijkstraStep? firstBackwardStep, out double totalCost);
            route.Add(firstBackwardStep.TargetNode);

            // logger.Debug("Dijkstra steps destination queue:");
            // while (dijkstraStepsDestination.TryDequeue(out DijkstraStep? step, out double priority))
            // {
            //     logger.Debug("Step Idx = {0},\tprevious node OsmId = {1},\ttarget node OsmId = {2},\tcumulated cost = {3},\tpriority = {4}",
            //         step?.Idx, step?.PreviousStep?.TargetNode?.OsmID, step?.TargetNode?.OsmID, step?.CumulatedCost, priority);
            // }

            logger.Info("Route reconstruction:");
            DijkstraStep? currentBackwardStep = firstBackwardStep;
            while(currentBackwardStep?.TargetNode?.OsmID != originNodeOsmId)
            {
                var nextBackwardStep = currentBackwardStep?.PreviousStep;
                route.Add(nextBackwardStep.TargetNode);
                currentBackwardStep = nextBackwardStep;
            }

            route.Reverse();

            foreach(var node in route)
            {
                logger.Info("Node OsmId = {0}", node.OsmID);
            } 


            return route;
        }

        private void CreateReducedDataSet(long originNodeOsmId, int maxNumberOfNodes)
        {
            int i = 0;
            int j = 0;

            Node? originNode = Array.Find(nodes, n => n.OsmID == originNodeOsmId);
            if(originNode == null)
            {
                logger.Info("Couldn't find origin node (source_osm = {0})", originNodeOsmId);
                return;
            }
            else
            {
                neighborhood.Add(originNode.Idx, originNode);
                reducedSetOfNodes[j] = originNode;
                logger.Info("Origin Node: OsmId={0}, Idx={1}", originNode?.OsmID, originNode?.Idx);
            }

            while(i < maxNumberOfNodes)
            {
                var node = reducedSetOfNodes[j];

                foreach(var outwardEdge in node.OutwardEdges)
                {
                    if(outwardEdge.TargetNode != null)
                    {
                        if(!neighborhood.ContainsKey(outwardEdge.TargetNode.Idx))
                        {
                            neighborhood.Add(outwardEdge.TargetNode.Idx, outwardEdge.TargetNode);
                            if(++i >= maxNumberOfNodes)
                            {
                                logger.Info("Maximum number of nodes reached");
                                return;
                            }
                            reducedSetOfNodes[i] = outwardEdge.TargetNode;
                        }
                    }
                }

                foreach(var inwardEdge in node.InwardEdges)
                {
                    if(inwardEdge.SourceNode != null)
                    {
                        if(!neighborhood.ContainsKey(inwardEdge.SourceNode.Idx))
                        {
                            neighborhood.Add(inwardEdge.SourceNode.Idx, inwardEdge.SourceNode);
                            if(++i >= maxNumberOfNodes)
                            {
                                logger.Info("Maximum number of nodes reached");
                                return;
                            }
                            reducedSetOfNodes[i] = inwardEdge.SourceNode;
                        }
                    }
                }
                if( i == j)
                {
                    logger.Info("No more neighbors connected to Node {0}", reducedSetOfNodes[i].Idx);
                    return;
                }
                j++;
            }
        }
    }
}