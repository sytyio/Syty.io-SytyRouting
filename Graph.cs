using Npgsql;
using NLog;
using System.Diagnostics;
using SytyRouting.Algorithms.KDTree;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Gtfs.GtfsUtils;

namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] NodesArray = new Node[0];
        private KDTree? KDTree;

        private Dictionary<int, byte> transportModeMasks = new Dictionary<int, byte>();

        public double MinCostPerDistance { get; private set; }
        public double MaxCostPerDistance { get; private set; }

        public Dictionary<string, ControllerGtfs> GtfsDico = new Dictionary<string, ControllerGtfs>();

        private Task FileSaveAsync(string path)
        {
            using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path)))
            {
                bw.Write(NodesArray.Length);
                foreach (var node in NodesArray)
                {
                    node.WriteToStream(bw);
                }
                var edgesArray = NodesArray.SelectMany(t => t.OutwardEdges).ToArray();
                bw.Write(edgesArray.Length);
                foreach (var edge in edgesArray)
                {
                    edge.WriteToStream(bw);
                }
                KDTree?.WriteToStream(bw);

                bw.Write(transportModeMasks.Count);
                foreach (int transportModeIndex in transportModeMasks.Keys)
                {
                    string transportModeName = Configuration.TransportModeNames[transportModeIndex];
                    bw.Write(transportModeName.Length);
                    for (int i = 0; i < transportModeName.Length; i++)
                    {
                        bw.Write((char)transportModeName[i]);
                    }
                }
                bw.Write(TransportModes.PublicTransportModes);
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
                    foreach (var edge in edgesArray)
                    {
                        edge.SourceNode.OutwardEdges.Add(edge);
                        edge.TargetNode.InwardEdges.Add(edge);
                    }

                    KDTree = new KDTree(br, NodesArray);

                    length = br.ReadInt32();
                    string[] transportModes = new string[length];
                    for (int i = 0; i < transportModes.Length; i++)
                    {
                        length = br.ReadInt32();
                        char[] tmc = new char[length];
                        for (int j = 0; j < length; j++)
                        {
                            tmc[j] = br.ReadChar();
                        }
                        transportModes[i] = new string(tmc);
                    }
                    byte publicTransportModes = br.ReadByte();

                    if (Configuration.VerifyTransportListFromGraphFile(transportModes))
                    {
                        transportModeMasks = TransportModes.CreateTransportModeMasks(transportModes);
                        TransportModes.SetPublicTransportModes(publicTransportModes);
                    }
                    else
                    {
                        throw new Exception("Transport Mode list from file differs from configuration.");
                    }

                }

                logger.Info("Loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
                stopWatch.Stop();
            }
            catch
            {
                logger.Info("Could not load from file, loading from DB instead.");
                // Initialise masks 
                await InitialiseMaskModes();


                await DBLoadAsync();
                KDTree = new KDTree(NodesArray);
                // ControllerGtfs.CleanGtfs();
                await AddGtfsData();
                KDTree = new KDTree(NodesArray);
                // ControllerGtfs.CleanGtfs();
                await FileSaveAsync(path);
            }
            ComputeCost();
        }

        private void ComputeCost()
        {
            MinCostPerDistance = double.MaxValue;
            MaxCostPerDistance = double.MinValue;
            for (int i = 0; i < NodesArray.Length; i++)
            {
                foreach (var edge in NodesArray[i].OutwardEdges)
                {
                    var distance = edge.LengthM;
                    if (distance > 0)
                    {
                        MinCostPerDistance = Math.Min(MinCostPerDistance, edge.Cost / distance);
                        MaxCostPerDistance = Math.Max(MaxCostPerDistance, edge.Cost / distance);
                    }
                }
            }
        }

        private async Task InitialiseMaskModes(){
            transportModeMasks = TransportModes.CreateTransportModeMasks(Configuration.TransportModeNames.Values.ToArray());
                await TransportModes.CreateMappingTagIdToTransportModes();
                TransportModes.SetPublicTransportModes(Configuration.PublicTransportModes);
                TransportModes.LoadTransportModeRoutingRules(Configuration.TransportModeRoutingRules);

        }

        private async Task DBLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Dictionary<long, Node> nodes = new Dictionary<long, Node>();

            var connectionString = Configuration.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Get the total number of rows to estimate the Graph creation time
            var totalDbRows = await Helper.DbTableRowCount(Configuration.EdgeTableName, logger);

            // Read all 'ways' rows and create the corresponding Nodes            
            //                     0        1      2       3         4          5      6   7   8   9       10          11         12        13            14                15            16
            var queryString = "SELECT osm_id, source, target, cost, reverse_cost, one_way, x1, y1, x2, y2, source_osm, target_osm, length_m, the_geom, maxspeed_forward, maxspeed_backward, tag_id FROM " + Configuration.EdgeTableName + " where length_m is not null"; // ORDER BY osm_id ASC LIMIT 10"; //  ORDER BY osm_id ASC LIMIT 10

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                int dbRowsProcessed = 0;

                while (await reader.ReadAsync())
                {
                    var sourceId = Convert.ToInt64(reader.GetValue(1)); // source
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

                    var length_m = Convert.ToDouble(reader.GetValue(12)); // length_m [m]
                    var theGeom = (LineString)reader.GetValue(13); // the_geom (?)
                    var maxSpeedForward_m_per_s = Convert.ToDouble(reader.GetValue(14)) * 1_000.0 / 60.0 / 60.0;  // maxspeed_forward [km/h]*[1000m/1km]*[1h/60min]*[1min/60s] = [m/s]
                    var maxSpeedBackward_m_per_s = Convert.ToDouble(reader.GetValue(15)) * 1_000.0 / 60.0 / 60.0;  // maxspeed_forward [km/h]*[1000m/1km]*[1h/60min]*[1min/60s] = [m/s]

                    var tagId = Convert.ToInt32(reader.GetValue(16)); // tag_id

                    CreateEdges(edgeOSMId, edgeCost, edgeReverseCost, edgeOneWay, source, target, length_m, theGeom, maxSpeedForward_m_per_s, maxSpeedBackward_m_per_s, tagId);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % 50000 == 0)
                    {
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        Helper.DataLoadBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                    }
                }

                NodesArray = nodes.Values.ToArray();
                for (int i = 0; i < NodesArray.Length; i++)
                {
                    NodesArray[i].Idx = i;
                }

                stopWatch.Stop();
                var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
                logger.Info("Graph creation time          (HH:MM:S.mS) :: " + totalTime);
                logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
                CleanGraph();
            }
        }

        public async Task GetDataFromGtfs()
        {
            foreach (var provider in Configuration.ProvidersInfo.Keys)
            {
                GtfsDico.Add(provider, new ControllerGtfs(provider));
            }
            List<Task> listDwnld = new List<Task>();
            foreach (var gtfs in GtfsDico)
            {
                listDwnld.Add(gtfs.Value.InitController());
            }
            await Task.WhenAll(listDwnld);
        }

        private async Task AddGtfsData()
        {
            await GetDataFromGtfs();
            foreach (var gtfs in GtfsDico)
            {
                // Connecting gtfs nodes to graph nodes
                foreach (var node in gtfs.Value.GetNodes())
                {
                    var nearest = KDTree.GetNearestNeighbor(node.X, node.Y);
                    var newEdgOut = new Edge { OsmID = long.MaxValue, SourceNode = node, TargetNode = nearest, LengthM = Helper.GetDistance(node, nearest), TransportModes = TransportModes.GetTransportModeMask("Foot") };
                    var newEdgeIn = new Edge { OsmID = long.MaxValue, SourceNode = nearest, TargetNode = node, LengthM = Helper.GetDistance(node, nearest), TransportModes = TransportModes.GetTransportModeMask("Foot") };
                    if (node.ValidSource)
                    {
                        node.OutwardEdges.Add(newEdgOut);
                    }
                    if (node.ValidTarget)
                    {
                        node.InwardEdges.Add(newEdgeIn);
                    }
                    nearest.InwardEdges.Add(newEdgOut);
                    nearest.OutwardEdges.Add(newEdgeIn);
                }
            }
            logger.Info("Nb nodes = {0} in graph", NodesArray.Count());
            foreach (var gtfs in GtfsDico)
            {
                var nodes = gtfs.Value.GetNodes().Union(gtfs.Value.GetInternalNodes()).ToArray();
                var testNode = gtfs.Value.GetNodes();
                var testInternNode = gtfs.Value.GetInternalNodes();
                NodesArray = NodesArray.Union(nodes).ToArray();
                logger.Info("Nb nodes = {0} in graph with the adding of {1} nodes ", NodesArray.Count(), gtfs.Key);
                logger.Info("Nb stop nodes = {0}, Nb internal nodes = {1}, Nb new nodes total = {2}", testNode.Count(), testInternNode.Count(), testNode.Count() + testInternNode.Count());
            }
            int i = 0;
            NodesArray.ToList().ForEach(x => x.Idx = i++);
        }

        public Node GetNodeByLongitudeLatitude(double x, double y, bool isTarget = false, bool isSource = false)
        {
            if (KDTree != null)
            {
                var node = KDTree.GetNearestNeighbor(x, y, isTarget, isSource);
                return node;
            }
            throw new Exception(String.Format("Impossible to find the nearest node based on the provided coordinates ({0},{1}). Optional parameters: isTarget = {2},  isSource = {3}", x, y, isTarget, isSource));
        }

        public Node GetNodeByOsmId(long osmId)
        {
            var node = Array.Find(NodesArray, n => n.OsmID == osmId);
            if (node == null)
            {
                logger.Debug("Node OsmId {0} not found", osmId);
                throw new ArgumentException(String.Format("Node OsmId {0} not found", osmId), "osmId");
            }

            return node;
        }

        public Node GetNodeByIndex(int index)
        {
            return NodesArray[index];
        }

        public int GetNodeCount()
        {
            return NodesArray.Length;
        }

        public Node[] GetNodes()
        {
            return NodesArray;
        }

        public void TraceOneNode(Node node)
        {
            logger.Info("Idx = {0}, OsmId =  {1}, nb in {2}, nb out {3}, idx {4}, coord = {5} {6}, T = {7}, s = {8}", node.Idx,
            node.OsmID, node.InwardEdges.Count, node.OutwardEdges.Count, node.Idx, node.Y, node.X, node.ValidTarget, node.ValidSource);
            TraceEdges(node);

            var availableInboundTransportModes = TransportModes.MaskToString(node.GetAvailableInboundTransportModes());
            var availableOutboundTransportModes = TransportModes.MaskToString(node.GetAvailableOutboundTransportModes());
            logger.Debug("Available Inbound Transport Modes for Node {0}: {1}", node.OsmID, availableInboundTransportModes);
            logger.Debug("Available Outbound Transport Modes for Node {0}: {1}", node.OsmID, availableOutboundTransportModes);
            logger.Debug("\n");
        }

        public void TraceNodes()
        {
            foreach (var node in NodesArray)
            {
                logger.Debug("Node Idx={0}, OsmID ={1}, X = {2}, Y = {3}",
                    node.Idx, node.OsmID, node.X, node.Y);
                TraceEdges(node);
            }
        }

        private void TraceEdges(Node node)
        {
            logger.Debug("\tInward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.InwardEdges)
            {
                TraceEdge(edge);
            }

            logger.Debug("\tOutward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.OutwardEdges)
            {
                TraceEdge(edge);
            }
        }

        private void TraceEdge(Edge edge)
        {
            logger.Info("\t\t > Edge: {0},\tcost: {1},\tSource Id: {2} ({3},{4});\tTarget Id: {5} ({6},{7});\tTransport Modes: {8} (mask: {9}) length = {10}",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.SourceNode?.X, edge.SourceNode?.Y, edge.TargetNode?.OsmID, edge.TargetNode?.X, edge.TargetNode?.Y, TransportModes.MaskToString(edge.TransportModes), edge.TransportModes,edge.LengthM);

            TraceInternalGeometry(edge);
        }

        private void TraceInternalGeometry(Edge edge)
        {
            if (edge.InternalGeometry is not null)
            {
                logger.Debug("\t\t   Internal geometry in Edge {0}:", edge.OsmID);
                foreach (var xymPoint in edge.InternalGeometry)
                {
                    logger.Debug("\t\t\tX: {0},\tY: {1},\tM: {2};",
                        xymPoint.X, xymPoint.Y, xymPoint.M);
                }
            }
            else
            {
                logger.Debug("\t\t   No Internal geometry in Edge {0}:", edge.OsmID);
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

        private void CreateEdges(long osmID, double cost, double reverse_cost, OneWayState oneWayState, Node source, Node target, double length_m, LineString geometry, double maxspeed_forward, double maxspeed_backward, int tagId)
        {
            byte transportModes = TransportModes.GetTransportModesForTagId(tagId);
            switch (oneWayState)
            {
                case OneWayState.Yes: // Only forward direction
                    {
                        var internalGeometry = Helper.GetInternalGeometry(geometry, oneWayState);
                        var edge = new Edge { OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_forward, TransportModes = transportModes };
                        source.OutwardEdges.Add(edge);
                        target.InwardEdges.Add(edge);

                        break;
                    }
                case OneWayState.Reversed: // Only backward direction
                    {
                        var internalGeometry = Helper.GetInternalGeometry(geometry, oneWayState);
                        var edge = new Edge { OsmID = osmID, Cost = reverse_cost, SourceNode = target, TargetNode = source, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_backward, TransportModes = transportModes };
                        source.InwardEdges.Add(edge);
                        target.OutwardEdges.Add(edge);

                        break;
                    }
                default: // Both ways
                    {
                        var internalGeometry = Helper.GetInternalGeometry(geometry, OneWayState.Yes);
                        var edge = new Edge { OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_forward, TransportModes = transportModes };
                        source.OutwardEdges.Add(edge);
                        target.InwardEdges.Add(edge);

                        internalGeometry = Helper.GetInternalGeometry(geometry, OneWayState.Reversed);
                        edge = new Edge { OsmID = osmID, Cost = reverse_cost, SourceNode = target, TargetNode = source, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_backward, TransportModes = transportModes };
                        source.InwardEdges.Add(edge);
                        target.OutwardEdges.Add(edge);

                        break;
                    }
            }
        }

        private void CleanGraph()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            logger.Info("Graph cleaning");
            foreach (var n in NodesArray)
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
            while (toProcess.TryDequeue(out node))
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
            logger.Info("Graph annotated and clean in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Stop();
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
