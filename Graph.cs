using NLog;
using System.Diagnostics;
using SytyRouting.Algorithms.KDTree;
using SytyRouting.Model;
using SytyRouting.DataBase;
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
                        TransportModes.LoadTransportModeRoutingRules(Configuration.TransportModeRoutingRules);
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


                await GetDbData();            

                KDTree = new KDTree(NodesArray);
                await AddGtfsData();
                KDTree = new KDTree(NodesArray);
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

        public async Task GetDbData(){
                DataBaseController db = new DataBaseController(Configuration.ConnectionString,Configuration.EdgeTableName);
                await db.InitController();
                NodesArray=db.GetNodes().ToArray();
                logger.Info("Nb nodes in graph = {0}",NodesArray.Count()); 
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
                var testEdges = gtfs.Value.GetEdges();
                NodesArray = NodesArray.Union(nodes).ToArray();
                logger.Info("Nb nodes = {0} in graph with the adding of {1} nodes ", NodesArray.Count(), gtfs.Key);
                logger.Info("Nb stop nodes = {0}, Nb internal nodes = {1}, Nb new nodes total = {2}", testNode.Count(), testInternNode.Count(), testNode.Count() + testInternNode.Count());
                logger.Info(" Nb edges = {0}",testEdges.Count());
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

        public void TraceNodes(Node[] nodesArray, int limit)
        {
            int nodesToDisplay = 0;
            if(limit<0 || limit > nodesArray.Length)
                nodesToDisplay = nodesArray.Length;
            else
                nodesToDisplay = limit;

            logger.Debug("Nodes to display: {0} (of {1} in total)", nodesArray.Length);
            
            
            for(var i = 0; i < nodesToDisplay; i++)
            {
                logger.Debug("Node Idx={0}, OsmID ={1}, X = {2}, Y = {3}",
                    nodesArray[i].Idx, nodesArray[i].OsmID, nodesArray[i].X, nodesArray[i].Y);
                TraceEdges(nodesArray[i]);
            }
        }

        public void TraceNodesByTransportMode(byte transportMode, int limit)
        {
            var filteredNodes = FilterNodesByAvailableTransportMode(transportMode);
            TraceNodes(filteredNodes, limit);
        }

        private Node[] FilterNodesByAvailableTransportMode(byte transportMode)
        {
            logger.Debug("Filtering nodes that contain the Transport Mode(s) {0}", TransportModes.MaskToString(transportMode));

            List<Node> filteredNodes = new List<Node>();
            
            foreach (var node in NodesArray)
            {
                var outwardEdges = node.OutwardEdges;
                foreach(var outwardEdge in outwardEdges)
                {
                    if((outwardEdge.TransportModes & transportMode)==transportMode)
                    {
                        filteredNodes.Add(node);
                    }
                }

                var inwardEdges = node.InwardEdges;
                foreach(var inwardEdge in inwardEdges)
                {
                    if((inwardEdge.TransportModes & transportMode)==transportMode)
                    {
                        filteredNodes.Add(node);
                    }
                }
            }

            return filteredNodes.ToArray();
        }

        private void TraceEdges(Node node)
        {
            logger.Info("\tInward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.InwardEdges)
            {
                TraceEdge(edge);
            }

            logger.Info("\tOutward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.OutwardEdges)
            {
                TraceEdge(edge);
            }
        }

        private void TraceEdge(Edge edge)
        {
            logger.Info("\t\t > Edge: {0},\tcost: {1},\tSource Id: {2} ({3},{4});\tTarget Id: {5} ({6},{7});\tTransport Modes: {8} (mask: {9}) length = {10} speed = {11}",
                    edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.SourceNode?.X, edge.SourceNode?.Y, edge.TargetNode?.OsmID, edge.TargetNode?.X, edge.TargetNode?.Y, TransportModes.MaskToString(edge.TransportModes), edge.TransportModes,edge.LengthM,edge.MaxSpeedMPerS);

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
