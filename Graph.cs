using Npgsql;
using NLog;
using System.Diagnostics;
namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Dictionary<long, Node> Nodes = new Dictionary<long, Node>();

        public async Task DBLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            // stopWatch.ElapsedMilliseconds;

            var connectionString = Constants.connectionString;
            string queryString;           

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Read all 'ways' rows and creates the corresponding Nodes
            //                     0      1      2       3           4            5      6   7   8   9
            queryString = "SELECT gid, source, target, cost_s, reverse_cost_s, one_way, x1, y1, x2, y2 FROM public.ways LIMIT 100";
            logger.Debug("DB query: {0}", queryString);

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {    
                ulong dbRowsProcessed = 0;

                while (await reader.ReadAsync())
                {
                    var sourceId = Convert.ToInt64(reader.GetValue(1)); // surce
                    var sourceX = Convert.ToDouble(reader.GetValue(6)); // x1
                    var sourceY = Convert.ToDouble(reader.GetValue(7)); // y1 
                    
                    var targetId = Convert.ToInt64(reader.GetValue(2)); // target
                    var targetX = Convert.ToDouble(reader.GetValue(8)); // x2
                    var targetY = Convert.ToDouble(reader.GetValue(9)); // y2
                    
                    var edgeId = Convert.ToInt64(reader.GetValue(0));   // gid
                    var edgeOneWay = (OneWayState)Convert.ToInt32(reader.GetValue(5)); // one_way

                    var sourceNode = CreateNode(sourceId, sourceX, sourceY);
                    var targetNode = CreateNode(targetId, targetX, targetY);

                    // Check for one_way state
                    switch (edgeOneWay)
                    {
                        case OneWayState.Yes: // Only forward direction
                            CreateEdge(edgeId, sourceNode, targetNode);
                            break;
                        case OneWayState.Reversed: // Only backward direction
                            CreateEdge(edgeId, targetNode, sourceNode);
                            break;
                        default: // Both ways
                            CreateEdge(edgeId, sourceNode, targetNode);
                            CreateEdge(edgeId, targetNode, sourceNode);
                            break;
                    }

                    dbRowsProcessed++;

                    if (dbRowsProcessed % Constants.logStopIterations == 0)
                    {
                        logger.Info("Number of DB rows already processed: {0}", dbRowsProcessed);
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

        private void GetEdges(long nodeId)
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
                logger.Trace("Node {0} is already in the Node collection", Nodes[id].Id);
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