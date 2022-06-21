using NLog;
using System.Diagnostics;
using System.Globalization;

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
            int j = 0;

            Dictionary<int, Node> visitedNodes = new Dictionary<int, Node>();
            Dictionary<int, DijkstraStep> dijkstraSteps = new Dictionary<int, DijkstraStep>();
            // Dictionary<int, DijkstraStep> dijkstraStepsOrdered = new Dictionary<int, DijkstraStep>();

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
                logger.Info("Origin Node: OsmId={0}, Idx={1}", originNode?.OsmID, originNode?.Idx);

                var firstStep = new DijkstraStep{Idx = originNode.OsmID, TargetNode = originNode, CumulatedCost = 0};
                dijkstraSteps.Add(i, firstStep);
                // dijkstraStepsOrdered.Add(j, firstStep);
                logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
                    i, dijkstraSteps[i].Idx, dijkstraSteps[i].PreviousStep?.TargetNode?.OsmID, dijkstraSteps[i].TargetNode?.OsmID, dijkstraSteps[i].CumulatedCost);
                
                // foreach(var node in reducedSetOfNodes)
                // {
                while(!visitedNodes.ContainsKey(destinationNode.Idx))
                {
                    var currentStep = dijkstraSteps[j];
                    var targetNode = currentStep.TargetNode;
                    if(targetNode != null)
                    {
                        foreach(var outwardEdge in targetNode.OutwardEdges)
                        {
                            if(!visitedNodes.ContainsKey(outwardEdge.TargetNode.Idx))
                            {
                                var dijkstraStep = new DijkstraStep{Idx = outwardEdge.TargetNode.OsmID, PreviousStep = currentStep, TargetNode = outwardEdge.TargetNode, CumulatedCost = outwardEdge.Cost + currentStep.CumulatedCost};
                                i++;
                                dijkstraSteps.Add(i, dijkstraStep);
                                logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
                                    i, dijkstraSteps[i].Idx, dijkstraSteps[i].PreviousStep?.TargetNode?.OsmID, dijkstraSteps[i].TargetNode?.OsmID, dijkstraSteps[i].CumulatedCost);
                            }
                        }
                        visitedNodes.Add(targetNode.Idx, targetNode);
                    }
                    j++;
                }
                logger.Debug("Destination node reached");
            }

            logger.Debug("Visited nodes:");
            foreach(var visitedNode in visitedNodes)
            {
                logger.Debug("Visited node OsmId = {0}", visitedNode.Value.OsmID);
            }

            logger.Debug("Dijkstra steps:");
            foreach(var step in dijkstraSteps)
            {
                logger.Debug("Step Key = {0},\tstep Idx = {1},\tprevious node OsmId = {2},\ttarget node OsmId = {3},\tcumulated cost = {4}",
                    step.Key, step.Value.Idx, step.Value.PreviousStep?.TargetNode?.OsmID, step.Value.TargetNode?.OsmID, step.Value.CumulatedCost);
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