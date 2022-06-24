using Npgsql;
using NLog;
using System.Diagnostics;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] NodesArray = new Node[0];
        private KDTree? KDTree;

        public Task FileSaveAsync(string path)
        {
            using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path)))
            {
                bw.Write(NodesArray.Length);
                foreach(var node in NodesArray)
                {
                    node.WriteToStream(bw);
                }
                var edgesArray = NodesArray.SelectMany(t => t.OutwardEdges).ToArray();
                bw.Write(edgesArray.Length);
                foreach(var edge in edgesArray)
                {
                    edge.WriteToStream(bw);
                }
                KDTree?.WriteToStream(bw);
            }
            return Task.CompletedTask;
        }

        public async Task FileLoadAsync(string path)
        {
            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    var length = br.ReadInt32();
                    NodesArray = new Node[length];
                    for (int i = 0; i < length; i++)
                        NodesArray[i] = new Node() { Idx = i };
                    for (int i = 0; i < length; i++)
                    {
                        NodesArray[i].ReadFromStream(br);
                    }
                    length = br.ReadInt32();
                    var edgesArray = new Edge[length];
                    for (int i = 0; i < length; i++)
                        edgesArray[i] = new Edge();
                    for (int i = 0; i < length; i++)
                    {
                        edgesArray[i].ReadFromStream(br, NodesArray);
                    }
                    foreach(var edge in edgesArray)
                    {
                        edge.SourceNode.OutwardEdges.Add(edge);
                        edge.TargetNode.InwardEdges.Add(edge);
                    }

                    KDTree = new KDTree(br, NodesArray);
                }
                logger.Info("Loaded in {0}", FormatElapsedTime(stopWatch.Elapsed));
                stopWatch.Stop();
            }
            catch
            {
                logger.Info("Could not load from file, loading from DB instead.");
                await DBLoadAsync();
                KDTree = new KDTree(NodesArray);
                await FileSaveAsync(path);
            }
        }

        public async Task DBLoadAsync()
        {
            Dictionary<long, Node> nodes = new Dictionary<long, Node>();
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
            //                     0        1      2       3         4          5      6   7   8   9    10           11
            queryString = "SELECT osm_id, source, target, cost, reverse_cost, one_way, x1, y1, x2, y2, source_osm, target_osm FROM public.ways";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {    
                long dbRowsProcessed = 0;

                while (await reader.ReadAsync())
                {
                    var sourceId = Convert.ToInt64(reader.GetValue(1)); // surce
                    var sourceX = Convert.ToDouble(reader.GetValue(6)); // x1
                    var sourceY = Convert.ToDouble(reader.GetValue(7)); // y1 
                    var sourceOSMId = Convert.ToInt64(reader.GetValue(10)); // source_osm
                    
                    var targetId = Convert.ToInt64(reader.GetValue(2)); // target
                    var targetX = Convert.ToDouble(reader.GetValue(8)); // x2
                    var targetY = Convert.ToDouble(reader.GetValue(9)); // y2
                    var targetOSMId = Convert.ToInt64(reader.GetValue(11)); // target_osm
                    
                    var edgeOSMId = Convert.ToInt64(reader.GetValue(0));  // gid
                    var edgeCost = Convert.ToDouble(reader.GetValue(3));  // cost
                    var edgeReverseCost = Convert.ToDouble(reader.GetValue(4)); // reverse_cost
                    var edgeOneWay = (OneWayState)Convert.ToInt32(reader.GetValue(5)); // one_way

                    var source = CreateNode(sourceId, sourceOSMId, sourceX, sourceY, nodes);
                    var target = CreateNode(targetId, targetOSMId, targetX, targetY, nodes);
                    
                    CreateEdges(edgeOSMId, edgeCost, edgeOneWay, source, target);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % Constants.stopIterations == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        GraphCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds);
                    }
                }

                NodesArray = nodes.Values.ToArray();
                for (int i = 0; i < NodesArray.Length; i++)
                {
                    NodesArray[i].Idx = i;
                }

                stopWatch.Stop();
                var totalTime = FormatElapsedTime(stopWatch.Elapsed);
                logger.Info("Graph creation time          (HH:MM:S.mS) :: " + totalTime);
                logger.Info("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
                CleanGraph();
            }
        }

        public Node GetNodeByOsmId(long osmId)
        {
            var node = Array.Find(NodesArray, n => n.OsmID == osmId);
            if(node == null)
            {
                logger.Info("Node OsmId {0} not found", osmId);
                throw new ArgumentException(String.Format( "Node OsmId {0} not found", osmId), "osmId");
            }

            return node;
        }

        public Node[] GetNodes()
        {            
            return NodesArray;
        }

        public void TraceNodes()
        {
            foreach (var node in NodesArray)
            {
                logger.Trace("Node Idx={0}, OsmID ={1}, X = {2}, Y = {3}",
                    node.Idx, node.OsmID, node.X, node.Y);
                TraceEdges(node);
            }
        }

        private void TraceEdges(Node node)
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

        private Node CreateNode(long id, long osmID, double x, double y, Dictionary<long, Node> nodes)
        {
            if (!nodes.ContainsKey(id))
            {
                var node = new Node { OsmID = osmID, X = x, Y = y };
                nodes.Add(id, node);
            }

            return nodes[id];
        }

        private void CreateEdges(long osmID, double cost, OneWayState oneWayState, Node source, Node target)
        {
            switch (oneWayState)
            {
                case OneWayState.Yes: // Only forward direction
                {
                    var edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target};
                    source.OutwardEdges.Add(edge);
                    target.InwardEdges.Add(edge);

                    break;
                }
                case OneWayState.Reversed: // Only backward direction
                {
                    var edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = target, TargetNode = source};
                    source.InwardEdges.Add(edge);
                    target.OutwardEdges.Add(edge);

                    break;
                }
                default: // Both ways
                {
                    var edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target};
                    source.OutwardEdges.Add(edge);
                    target.InwardEdges.Add(edge);

                    edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = target, TargetNode = source};
                    source.InwardEdges.Add(edge);
                    target.OutwardEdges.Add(edge);
                    
                    break;
                }
            }
        }

        private void GraphCreationBenchmark(long totalDbRows, long dbRowsProcessed, TimeSpan timeSpan, long timeSpanMilliseconds)
        {
            var elapsedTime = FormatElapsedTime(timeSpan);

            var rowProcessingRate = (double)dbRowsProcessed / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var graphCreationTimeSeconds = totalDbRows / rowProcessingRate;
            var graphCreationTime = TimeSpan.FromSeconds(graphCreationTimeSeconds);

            var totalTime = FormatElapsedTime(graphCreationTime);

            logger.Info("Number of DB rows already processed: {0}", dbRowsProcessed);
            logger.Info("Row processing rate: {0} [Rows / s]", rowProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            logger.Info("Elapsed Time                 (HH:MM:S.mS) :: " + elapsedTime);
            logger.Info("Graph creation time estimate (HH:MM:S.mS) :: " + totalTime);
        }

        private void CleanGraph()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            logger.Info("Graph cleaning");
            foreach(var n in NodesArray)
            {
                n.ValidSource = false;
                n.ValidTarget = false;
            }
            var toProcess = new Queue<Node>();
            var root = NodesArray.First();
            root.ValidSource = true;
            root.ValidTarget = true;
            toProcess.Enqueue(root);
            Node? node;
            while(toProcess.TryDequeue(out node))
            {
                if (node.ValidSource)
                {
                    foreach (var neighbor in node.InwardEdges)
                    {
                        if (!neighbor.SourceNode.ValidSource)
                        {
                            neighbor.SourceNode.ValidSource = true;
                            toProcess.Enqueue(neighbor.SourceNode);
                        }
                    }
                }
                if (node.ValidTarget)
                {
                    foreach (var neighbor in node.OutwardEdges)
                    {
                        if (!neighbor.TargetNode.ValidTarget)
                        {
                            neighbor.TargetNode.ValidTarget = true;
                            toProcess.Enqueue(neighbor.TargetNode);
                        }
                    }
                }
            }
            logger.Info("Graph annotated and clean in {0}", FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Stop();
        }

        private string FormatElapsedTime(TimeSpan timeSpan)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds);

            return elapsedTime;
        }

        public void TestClosestNode(string name, double x, double y)
        {
            if (KDTree != null)
            {
                var node = KDTree.GetNearestNeighbor(x, y);
                logger.Info("Closest node to {0} has OSM ID {1}", name, node.OsmID);

                var adjacentIds = node.OutwardEdges.Union(node.InwardEdges).Select(t => t.OsmID).Distinct();
                foreach (var itm in adjacentIds)
                {
                    logger.Info("     => Adjacent road has OSM ID {1}", name, itm);
                }
            }
        }
    }
}
