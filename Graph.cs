using Npgsql;
using NLog;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Globalization;

namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Dictionary<long, Node> Nodes = new Dictionary<long, Node>();


        public Task FileSaveAsync(string path)
        {
            using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path)))
            {
                var array = Nodes.Values.ToArray();
                var pos = Enumerable.Range(0, array.Length).ToDictionary(t => array[t].Id);
                bw.Write(array.Length);
                foreach(var node in array)
                {
                    node.WriteToStream(bw, pos);
                }
            }
            return Task.CompletedTask;
        }

        public async Task FileLoadAsync(string path)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    Nodes = new Dictionary<long, Node>();
                    var length = br.ReadInt32();
                    var array = new Node[length];
                    for (int i = 0; i < length; i++)
                        array[i] = new Node();
                    for (int i = 0; i < length; i++)
                    {
                        array[i].ReadFromStream(br, array);
                    }

                    Nodes = array.ToDictionary(t => t.Id);
                }
            }
            catch
            {
                logger.Info("Could not load from file, loading from DB instead.");
                await DBLoadAsync();
                await FileSaveAsync(path);
            }
        }

        public async Task DBLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.connectionString;
            string queryString;           

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Get the total number of rows to estimate the Graph creation time
            long totalDbRows = 0;
            queryString = "SELECT count(*) AS exact_count FROM public.ways";
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    totalDbRows = Convert.ToInt64(reader.GetValue(0));
                }
            }

            logger.Info("Total number of rows to process: {0}", totalDbRows);

            // Read all 'ways' rows and creates the corresponding Nodes            
            //                     0      1      2       3         4          5      6   7   8   9
            // queryString = "SELECT gid, source, target, cost, reverse_cost, one_way, x1, y1, x2, y2 FROM public.ways";
            queryString = "SELECT gid, source, target, cost, reverse_cost, one_way, x1, y1, x2, y2 FROM public.ways ORDER BY source LIMIT 100"; // ORDER BY and LIMIT are for testing only
            logger.Debug("DB query: {0}", queryString);

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {    
                long dbRowsProcessed = 0;

                while (await reader.ReadAsync())
                {
                    var sourceId = Convert.ToInt64(reader.GetValue(1)); // surce
                    var sourceX = Convert.ToDouble(reader.GetValue(6)); // x1
                    var sourceY = Convert.ToDouble(reader.GetValue(7)); // y1 
                    
                    var targetId = Convert.ToInt64(reader.GetValue(2)); // target
                    var targetX = Convert.ToDouble(reader.GetValue(8)); // x2
                    var targetY = Convert.ToDouble(reader.GetValue(9)); // y2
                    
                    var edgeId = Convert.ToInt64(reader.GetValue(0));   // gid
                    var edgeCost = Convert.ToDouble(reader.GetValue(3)); // cost
                    var edgeReverseCost = Convert.ToDouble(reader.GetValue(4)); // reverse_cost
                    var edgeOneWay = (OneWayState)Convert.ToInt32(reader.GetValue(5)); // one_way

                    var sourceNode = CreateNode(sourceId, sourceX, sourceY);
                    var targetNode = CreateNode(targetId, targetX, targetY);

                    // Check for one_way state
                    switch (edgeOneWay)
                    {
                        case OneWayState.Yes: // Only forward direction
                            CreateEdge(edgeId, edgeCost, sourceNode, targetNode);
                            break;
                        case OneWayState.Reversed: // Only backward direction
                            CreateEdge(edgeId, edgeReverseCost, targetNode, sourceNode);
                            break;
                        default: // Both ways
                            CreateEdge(edgeId, edgeCost, sourceNode, targetNode);
                            CreateEdge(edgeId, edgeReverseCost, targetNode, sourceNode);
                            break;
                    }

                    dbRowsProcessed++;

                    if (dbRowsProcessed % Constants.stopIterations == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        GraphCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds);
                    }
                }
                stopWatch.Stop();
                logger.Info("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
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
                logger.Debug("\t\tEdge: {0}, Cost: {1}, End Node Id: {2};", edge.Id, edge.Cost, edge.EndNode?.Id);
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

        private void CreateEdge(long edgeId, double cost, Node baseNode, Node endNode)
        {
            var edge = new Edge{Id = edgeId, Cost = cost, EndNode = endNode};
            baseNode.TargetEdges.Add(edge);
            logger.Trace("Edge {0} was successfully added to Node {1}", edgeId, baseNode.Id);
        }

        private void GraphCreationBenchmark(long totalDbRows, long dbRowsProcessed, TimeSpan timeSpan, long timeSpanMilliseconds)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds);

            var rowProcessingRate = (double)dbRowsProcessed / timeSpanMilliseconds * 1000;
            var graphCreationTimeSeconds = totalDbRows / rowProcessingRate;
            var graphCreationTime = TimeSpan.FromSeconds(graphCreationTimeSeconds);

            string totalTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                graphCreationTime.Hours, graphCreationTime.Minutes, graphCreationTime.Seconds,
                graphCreationTime.Milliseconds);

            logger.Info("Number of DB rows already processed: {0}", dbRowsProcessed);
            logger.Info("Row processing rate: {0} [Rows / s]", rowProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            logger.Info("Elapsed Time                 (HH:MM:S.mS) :: " + elapsedTime);
            logger.Info("Graph creation time estimate (HH:MM:S.mS) :: " + totalTime);
        }
    }
}
