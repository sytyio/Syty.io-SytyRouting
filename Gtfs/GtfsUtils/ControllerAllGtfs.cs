
using NLog;
using SytyRouting.Model;
using SytyRouting.Algorithms.KDTree;


namespace SytyRouting.Gtfs.GtfsUtils
{
    public class ControllerAllGtfs : ControllerExternalSource
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public Dictionary<string, ControllerGtfs> GtfsDico = new Dictionary<string, ControllerGtfs>();
        private Node[] NodesArray;

        public KDTree? KDTree;

        public ControllerAllGtfs(KDTree? kDTree, Node [] nodesArray){
            KDTree=kDTree;
            NodesArray=nodesArray;

        }

        public void Clean()
        {
            if (Directory.Exists("GtfsData"))
            {
                Directory.Delete("GtfsData", true);
                logger.Info("Cleaning GtfsData");
            }
            else
            {
                logger.Info("No data found");
            }
        }

        public IEnumerable<Edge> GetEdges()
        {
            return new List<Edge>();
        }

        public IEnumerable<Node> GetInternalNodes()
        {
            return new List<Node>();
        }

        public IEnumerable<Node> GetNodes()
        {
            return NodesArray;
        }

        public async Task InitController()
        {
            Clean();
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
            AddGtfsData();
            Clean();
        }

        private void AddGtfsData()
        {
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
            foreach (var gtfs in GtfsDico)
            {
                var nodes = gtfs.Value.GetNodes().Union(gtfs.Value.GetInternalNodes()).ToArray();
                var testNode = gtfs.Value.GetNodes();
                var testInternNode = gtfs.Value.GetInternalNodes();
                var testEdges = gtfs.Value.GetEdges();
                NodesArray = NodesArray.Union(nodes).ToArray();
                logger.Info("Nb nodes = {0} in graph with the adding of {1} nodes ", NodesArray.Count(), gtfs.Key);
                logger.Info("Nb stop nodes = {0}, Nb internal nodes = {1}, Nb new nodes total = {2}", testNode.Count(), testInternNode.Count(), testNode.Count() + testInternNode.Count());
            }
            int i = 0;
            NodesArray.ToList().ForEach(x => x.Idx = i++);
        }
    }
}