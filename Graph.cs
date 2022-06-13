using Npgsql;

namespace SytyRouting
{
    public class Graph
    {
        public Dictionary<long, Node> Nodes = new Dictionary<long, Node>();

        public async Task DBLoadAsync()
        {
            var connectionString = Constants.connectionString;
            string queryString;           

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Read all 'ways' rows and creates the corresponding Nodes
            queryString = "SELECT * FROM public.ways ORDER BY source ASC LIMIT 100";
            
            Console.WriteLine("");
            Console.WriteLine(queryString);
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

                    Console.WriteLine("Query result:: source: id={0}, x={1}, y={2}; target: id={3}, x={4}, y={5}", sourceId, sourceX, sourceY, targetId, targetX, targetY);

                    // If it is not already in the Node dictionary, creates a Node based on the 'source' information
                    sourceNode = CreateNode(sourceId, sourceX, sourceY);
                    
                    // If it is not already in the Node dictionary, creates a Node based on the 'target' information
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
                Console.WriteLine("Nodes Key(Node Id) = {0}({1}), X = {2}, Y = {3}",
                    node.Key, node.Value.Id, node.Value.X, node.Value.Y);
            }
        }

        private Node CreateNode(long id, double x, double y)
        {
            if (!Nodes.ContainsKey(id))
            {   
                var node = new Node{Id = id, X = x, Y = y};
                Nodes.Add(id, node);
                Console.WriteLine("New Node added for key = {0} (nodeId)", Nodes[id].Id);
            }
            else
            {
                Console.WriteLine("The node {0} is already in the Node Dictionary", Nodes[id].Id);
            }

            return Nodes[id];
        }

        private void CreateEdge(long edgeId, Node baseNode, Node endNode)
        {
            var edge = new Edge{Id = edgeId, EndNode = endNode};
            baseNode.TargetEdges.Add(edge);
            Console.WriteLine("Edge {0} was successfully added to Node {1}", edgeId, baseNode.Id);
        }
    }
}