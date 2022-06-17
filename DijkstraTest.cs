using NLog;
using System.Diagnostics;
using System.Globalization;

namespace SytyRouting
{
    public class DijkstraTest
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Create a reduced graph for testing:
        private Node[] nodes =  new Node[0];
        private Node[] reducedSetOfNodes =  new Node[0];
        private Dictionary<int, Node> neighborhood = new Dictionary<int, Node>();


        public DijkstraTest(Node[] graphNodes)
        {
            nodes = graphNodes;
        }

        public Node[] GetNodes()
        {
            foreach (var node in reducedSetOfNodes)
            {
                logger.Trace("Node {0}({1}), X = {2}, Y = {3}",
                    node.OsmID, node.X, node.Y);
                GetEdges(node);
            }
            
            return reducedSetOfNodes;
        }

        private void GetEdges(Node node)
        {
            logger.Trace("\tInward Edges in Node {0}:", node.OsmID);
            foreach(var edge in node.InwardEdges)
            {
                logger.Trace("\t\tEdge: {0},\tcost: {1},\tsource Node Id: {2},\ttarget Node Id: {3};",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.TargetNode?.OsmID);
            }
            
            logger.Trace("\tOutward Edges in Node {0}:", node.OsmID);
            foreach(var edge in node.OutwardEdges)
            {
                logger.Trace("\t\tEdge: {0},\tcost: {1},\tsource Node Id: {2},\ttarget Node Id: {3};",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.TargetNode?.OsmID);
            }
        }

        public void CreateReducedDataSet(int originNodeIdx, int maxNumberOfNodes)
        {
            int i = 0;
            int Idx = originNodeIdx;
            neighborhood.Add(Idx, nodes[Idx]);

            while(i < maxNumberOfNodes)
            {
                i = i + GetNeighborNodes(neighborhood[Idx], Idx);
            }
            reducedSetOfNodes = neighborhood.Values.ToArray();
        }

        private int GetNeighborNodes(Node node, int Idx)
        {
            int i = 0;
            foreach(Edge outwardEdge in node.OutwardEdges)
            {
                if(outwardEdge.TargetNode != null)
                {
                    if(!neighborhood.ContainsKey(outwardEdge.TargetNode.Idx))
                        neighborhood.Add(outwardEdge.TargetNode.Idx, outwardEdge.TargetNode);
                    i++;
                }
            }

            foreach(Edge inwardEdge in node.InwardEdges)
            {
                if(inwardEdge.SourceNode != null)
                {
                    if(!neighborhood.ContainsKey(inwardEdge.SourceNode.Idx))
                        neighborhood.Add(inwardEdge.SourceNode.Idx, inwardEdge.SourceNode);
                    i++;
                }
            }

            return i;
        }
    }
}