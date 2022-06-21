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