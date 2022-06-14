using Npgsql;
using NLog;

namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Dictionary<long, Node> Nodes = new Dictionary<long, Node>();

        public async Task DBLoadAsync()
        {
            var connectionString = Constants.connectionString;
            string queryString;           

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Read all 'ways' rows and creates the corresponding Nodes
            queryString = "SELECT * FROM public.ways ORDER BY source ASC LIMIT 100";
            logger.Debug("DB query: {0}", queryString);

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long sourceId;
                double sourceX;
                double sourceY;

                long targetId;
                double targetX;
                double targetY;

                long edgeId;
                Constants.OneWayState edgeOneWay;
                
                Node sourceNode = new Node();
                Node targetNode = new Node();

                while (await reader.ReadAsync())
                {
                    sourceId = Convert.ToInt64(reader.GetValue(6));
                    sourceX = Convert.ToDouble(reader.GetValue(17));
                    sourceY = Convert.ToDouble(reader.GetValue(18));
                    
                    targetId = Convert.ToInt64(reader.GetValue(7));
                    targetX = Convert.ToDouble(reader.GetValue(19));
                    targetY = Convert.ToDouble(reader.GetValue(20));
                    
                    edgeId = Convert.ToInt64(reader.GetValue(0));
                    edgeOneWay = (Constants.OneWayState)Convert.ToInt32(reader.GetValue(15));

                    sourceNode = CreateNode(sourceId, sourceX, sourceY);
                    targetNode = CreateNode(targetId, targetX, targetY);

                    // Check for one_way state
                    switch (edgeOneWay)
                    {
                        case Constants.OneWayState.Yes: // Only forward direction
                            CreateEdge(edgeId, sourceNode, targetNode);
                            break;
                        case Constants.OneWayState.Reversed: // Only backward direction
                            CreateEdge(edgeId, targetNode, sourceNode);
                            break;
                        default: // Both ways
                            CreateEdge(edgeId, sourceNode, targetNode);
                            CreateEdge(edgeId, targetNode, sourceNode);
                            break;
                    }
                }
            }
        }

        public void GetNodes()
        {
            foreach(var node in this.Nodes)
            {
                logger.Debug("Node {0}({1}), X = {2}, Y = {3}",
                    node.Key, node.Value.Id, node.Value.X, node.Value.Y);
                GetEdges(node.Value.Id);
            }
            
        }

        public void GetEdges(long nodeId)
        {
            logger.Debug("\tEdges in Node {0}:", nodeId);
            foreach(var edge in Nodes[nodeId].TargetEdges)
            {
                logger.Debug("\t\tEdge: {0}, End Node Id: {1};", edge.Id, edge.EndNode?.Id);
            }
        }

        private Node CreateNode(long id, double x, double y)
        {
            if (!Nodes.ContainsKey(id))
            {   
                var node = new Node{Id = id, X = x, Y = y};
                Nodes.Add(id, node);
                logger.Trace("New Node added for key (nodeId) = {0} ", Nodes[id].Id);
            }
            else
            {
                logger.Warn("Node {0} is already in the Node collection", Nodes[id].Id);
            }

            return Nodes[id];
        }

        private void CreateEdge(long edgeId, Node baseNode, Node endNode)
        {
            var edge = new Edge{Id = edgeId, EndNode = endNode};
            baseNode.TargetEdges.Add(edge);
            logger.Trace("Edge {0} was successfully added to Node {1}", edgeId, baseNode.Id);
        }
    }
}