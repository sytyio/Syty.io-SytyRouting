using Npgsql;
using NLog;
using System.Diagnostics;
using SytyRouting.Algorithms.KDTree;
using SytyRouting.Model;
using NetTopologySuite.Geometries;


namespace SytyRouting
{
    public class Graph
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] NodesArray = new Node[0];
        private KDTree? KDTree;

        private Dictionary<int,ushort> tagIdToTransportMode = new Dictionary<int,ushort>();
        private Dictionary<String,ushort> transportModeMasks = new Dictionary<String,ushort>();

        public double MinCostPerDistance { get; private set; }
        public double MaxCostPerDistance { get; private set; }

        private Task FileSaveAsync(string path)
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

                bw.Write(transportModeMasks.Count);
                // foreach(KeyValuePair<string, ushort> mask in transportModeMasks)
                // {
                //     bw.Write(mask.Key.Length);
                //     for(int i = 0; i < mask.Key.Length; i++)
                //     {
                //         bw.Write((char)mask.Key[i]);
                //     }
                //     bw.Write(mask.Value);
                // }
                foreach(string transportMode in transportModeMasks.Keys)
                {
                    bw.Write(transportMode.Length);
                    for(int i = 0; i < transportMode.Length; i++)
                    {
                        bw.Write((char)transportMode[i]);
                    }
                }
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

                    length = br.ReadInt32();
                    string[] transportModes = new string[length];
                    for(int i = 0; i < transportModes.Length; i++)
                    {
                        length = br.ReadInt32();
                        char[] tmc = new char[length];
                        for(int j = 0; j < length; j++)
                        {
                            tmc[j] =  br.ReadChar();
                        }
                        transportModes[i] = new string(tmc);
                    }
                    CreateTransportModeMasks(transportModes);

                }
                logger.Info("Loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
                stopWatch.Stop();
            }
            catch
            {
                logger.Info("Could not load from file, loading from DB instead.");
                await DBLoadAsync();
                KDTree = new KDTree(NodesArray);
                await FileSaveAsync(path);
            }
            ComputeCost();
        }

        private void ComputeCost()
        {
            MinCostPerDistance = double.MaxValue;
            MaxCostPerDistance = double.MinValue;
            for (int i = 0; i < NodesArray.Length;i++)
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

        private async Task DBLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Dictionary<long, Node> nodes = new Dictionary<long, Node>();

            var connectionString = Configuration.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            CreateTransportModeMasks(Configuration.TransportModeNames);
            await CreateMappingTagIdToTransportMode();

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

        public Node GetNodeByLongitudeLatitude(double x, double y)
        {
            if (KDTree != null)
            {
                var node = KDTree.GetNearestNeighbor(x, y);
                return node;
            }
            throw new Exception("Impossible to find the nearest node based on the provided coordinates.");
        }

        public Node GetNodeByOsmId(long osmId)
        {
            var node = Array.Find(NodesArray, n => n.OsmID == osmId);
            if(node == null)
            {
                logger.Debug("Node OsmId {0} not found", osmId);
                throw new ArgumentException(String.Format( "Node OsmId {0} not found", osmId), "osmId");
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
            foreach(var edge in node.InwardEdges)
            {
                TraceEdge(edge);
            }
            
            logger.Debug("\tOutward Edges in Node {0}:", node.OsmID);
            foreach(var edge in node.OutwardEdges)
            {
                TraceEdge(edge);
            }
        }

        private void TraceEdge(Edge edge)
        {
            logger.Debug("\t\tEdge: {0},\tcost: {1},\tsource Node Id: {2} ({3},{4});\ttarget Node Id: {5} ({6},{7});\tTransport Modes: {8} ({9})",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.SourceNode?.X, edge.SourceNode?.Y, edge.TargetNode?.OsmID, edge.TargetNode?.X, edge.TargetNode?.Y, TransportModesToString(edge.TransportModes), edge.TransportModes);
            
            TraceInternalGeometry(edge);
        }

        private void TraceInternalGeometry(Edge edge)
        {
            if(edge.InternalGeometry is not null)
            {
                logger.Debug("\t\tInternal geometry in Edge {0}:", edge.OsmID);
                foreach(var xymPoint in edge.InternalGeometry)
                {
                    logger.Debug("\t\t\tX: {0},\tY: {1},\tM: {2};",
                        xymPoint.X, xymPoint.Y, xymPoint.M);
                }
            }
            else
            {
                logger.Debug("\t\tNo Internal geometry in Edge {0}:", edge.OsmID);
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
            ushort transportModes = GetTransportModes(tagId);
            switch (oneWayState)
            {
                case OneWayState.Yes: // Only forward direction
                {
                    var internalGeometry = GetInternalGeometry(geometry, oneWayState);
                    var edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_forward, TransportModes = transportModes};
                    source.OutwardEdges.Add(edge);
                    target.InwardEdges.Add(edge);

                    break;
                }
                case OneWayState.Reversed: // Only backward direction
                {
                    var internalGeometry = GetInternalGeometry(geometry, oneWayState);
                    var edge = new Edge{OsmID = osmID, Cost = reverse_cost, SourceNode = target, TargetNode = source, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_backward, TransportModes = transportModes};
                    source.InwardEdges.Add(edge);
                    target.OutwardEdges.Add(edge);

                    break;
                }
                default: // Both ways
                {
                    var internalGeometry = GetInternalGeometry(geometry, OneWayState.Yes);
                    var edge = new Edge{OsmID = osmID, Cost = cost, SourceNode = source, TargetNode = target, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_forward, TransportModes = transportModes};
                    source.OutwardEdges.Add(edge);
                    target.InwardEdges.Add(edge);

                    internalGeometry = GetInternalGeometry(geometry, OneWayState.Reversed);
                    edge = new Edge{OsmID = osmID, Cost = reverse_cost, SourceNode = target, TargetNode = source, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_backward, TransportModes = transportModes};
                    source.InwardEdges.Add(edge);
                    target.OutwardEdges.Add(edge);
                    
                    break;
                }
            }
        }

        private void CreateTransportModeMasks(string[] transportModes)
        {
            // Create bitmasks for the Transport Modes based on the configuration data using a Dictionary.
            try
            {
                transportModeMasks.Add(transportModes[0],0);
                for(int n = 0; n < transportModes.Length-1; n++)
                {
                    var twoToTheNth = (byte)Math.Pow(2,n);
                    var transportName = transportModes[n+1];
                    transportModeMasks.Add(transportName,twoToTheNth);
                }
                foreach(var tmm in transportModeMasks)
                {
                    Console.WriteLine("{0}: {1}", tmm.Key,tmm.Value);
                }
            }
            catch (Exception e)
            {
                logger.Info("Transport Mode bitmask creation error: {0}", e.Message);
            }
        }

        private string TransportModesToString(int transportModes)
        {
            string result = "";
            foreach(var tmm in transportModeMasks)
            {
                if(tmm.Value != 0 && (transportModes & tmm.Value) == tmm.Value)
                {
                    result += tmm.Key + ", ";
                }
            }
            return (result == "")? Configuration.TransportModeNames[0]: result;
        }

        private ushort GetTransportModes(int tagId)
        {
            if (tagIdToTransportMode.ContainsKey(tagId))
            {
                return tagIdToTransportMode[tagId];
            }
            else
            {
                logger.Info("Unable to find OSM tag_id {0} in the tag_id-to-Transport Mode mapping. Transport Mode set to 'None'", tagId);
                return (ushort)0; // Default Ttransport Mode: 0 ("None");
            }
        }

        private async Task CreateMappingTagIdToTransportMode()
        {
            tagIdToTransportMode = await Configuration.CreateMappingTagIdToTransportMode(transportModeMasks);

            foreach(var ti2tmm in tagIdToTransportMode)
            {
                Console.WriteLine("{0}: {1} :: {2}", ti2tmm.Key,ti2tmm.Value,TransportModesToString(ti2tmm.Value));
            }
        }

        private XYMPoint[]? GetInternalGeometry(LineString geometry, OneWayState oneWayState)
        {
            if(geometry.Count > 2)
            {
                Coordinate[] coordinates = geometry.Coordinates;
                            
                if(oneWayState == OneWayState.Reversed)
                    coordinates = coordinates.Reverse().ToArray();
                
                var fullGeometry = new XYMPoint[coordinates.Length];
                
                for(int c = 0; c < coordinates.Length; c++)
                {
                    XYMPoint xYMPoint;

                    xYMPoint.X = coordinates[c].X;
                    xYMPoint.Y = coordinates[c].Y;
                    xYMPoint.M = 0;

                    fullGeometry[c] = xYMPoint;
                }

                CalculateCumulativeDistance(fullGeometry, fullGeometry.Length-1);
                NormalizeGeometry(fullGeometry);

                var internalGeometry = new XYMPoint[coordinates.Length-2];
                for(var i = 0; i < internalGeometry.Length; i++)
                {
                    internalGeometry[i] = fullGeometry[i+1];
                }
                
                return internalGeometry;
            }
            else
            {
                return null;
            }
        }

        private double CalculateCumulativeDistance(XYMPoint[] internalGeometry, int index)
        {
            while(index > 0 && index < internalGeometry.Length)
            {
                var x1 = internalGeometry[index].X;
                var y1 = internalGeometry[index].Y;
                var x2 = internalGeometry[index-1].X;
                var y2 = internalGeometry[index-1].Y;
                var distance = Helper.GetDistance(x1, y1, x2, y2);

                var cumulativeDistance = distance + CalculateCumulativeDistance(internalGeometry, index-1);

                internalGeometry[index].M = cumulativeDistance;

                return cumulativeDistance;
            }
            return 0;
        }

        private void NormalizeGeometry(XYMPoint[] geometry)
        {
            double normalizationParameter = geometry.Last().M;
            for(int g = 0; g < geometry.Length; g++)
            {
                geometry[g].M = geometry[g].M / normalizationParameter;
            }
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
