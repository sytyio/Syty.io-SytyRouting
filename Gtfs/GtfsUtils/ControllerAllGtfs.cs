
using NLog;
using SytyRouting.Model;
using SytyRouting.Algorithms.KDTree;
using System.Diagnostics;

namespace SytyRouting.Gtfs.GtfsUtils
{
    public class ControllerAllGtfs : ControllerExternalSource
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public Dictionary<string, ControllerGtfs> GtfsDico = new Dictionary<string, ControllerGtfs>();
        private Node[] NodesArray;

        public KDTree? KDTree;

        public ControllerAllGtfs(KDTree? kDTree, Node [] nodesArray)
        {
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
                var gtfsValueInitCtrl = gtfs.Value.InitController(); 
                listDwnld.Add(gtfsValueInitCtrl);
            }
            await Task.WhenAll(listDwnld);
            AddGtfsData();
            Clean();
        }

        private void AddGtfsData()
        {
            if(KDTree is not null)
            {
                foreach (var gtfs in GtfsDico)
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    // Connecting gtfs nodes to graph nodes
                    int nodesProcessed = 0;
                    int totalNodes = gtfs.Value.GetNodes().Count();
                    
                    foreach (var node in gtfs.Value.GetNodes())
                    {
                        var nearest = KDTree.GetNearestNeighbor(node.X, node.Y)!;
                        var length = Helper.GetDistance(node, nearest);
                        
                        // Perhaps a config setting instead of a die-hard 1?
                        if(length==0){
                            length=Configuration.NotNullDistanceFootTransitions; // Average edge type 13 length: 5.327618825484594 [m]
                        }
                        
                        var newEdgOut = new Edge { OsmID = long.MaxValue, SourceNode = node, TargetNode = nearest, LengthM = length, TransportModes = TransportModes.DefaultMode,MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.DefaultMode], TagIdRouteType=TransportModes.GtfsDefaultFoot };
                        var newEdgeIn = new Edge { OsmID = long.MaxValue, SourceNode = nearest, TargetNode = node, LengthM = length, TransportModes = TransportModes.DefaultMode,MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.DefaultMode], TagIdRouteType=TransportModes.GtfsDefaultFoot };

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
                        nodesProcessed++;
                        if(nodesProcessed%1000==0){
                            var timeSpan = stopWatch.Elapsed;
                            Helper.DataLoadBenchmark(totalNodes, nodesProcessed, timeSpan, logger);
                        }
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
            else
            {
                throw new Exception("KDTree failure on AddGtfsData()");
            }
        }
    }
}