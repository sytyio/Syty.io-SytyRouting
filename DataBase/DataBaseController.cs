using System.Diagnostics;
using NetTopologySuite.Geometries;
using NLog;
using Npgsql;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{

    public class DataBaseController : ControllerExternalSource
    {
        
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Node[] nodesArray = new Node[0];

        public string ConnectionString;

        public string EdgeTableName;

        public DataBaseController(string connection, string edgeTableName){
                ConnectionString = connection;
                EdgeTableName=edgeTableName;
        }

        private async Task DBLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Dictionary<long, Node> nodes = new Dictionary<long, Node>();

            // connectionString = Configuration.ConnectionString;

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Get the total number of rows to estimate the Graph creation time
            var totalDbRows = await Helper.DbTableRowCount(EdgeTableName, logger);

            // Read all 'ways' rows and create the corresponding Nodes            
            //                             0       1       2     3             4        5   6   7   8   9          10          11        12        13                14                 15      16
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

                nodesArray = nodes.Values.ToArray();
                for (int i = 0; i < nodesArray.Length; i++)
                {
                    nodesArray[i].Idx = i;
                }

                stopWatch.Stop();
                var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
                logger.Info("Graph creation time          (HH:MM:S.mS) :: " + totalTime);
                logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
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
                        var edge = new Edge { OsmID = osmID, Cost = cost, OneWayState = oneWayState, SourceNode = source, TargetNode = target, LengthM = length_m, InternalGeometry = internalGeometry, MaxSpeedMPerS = maxspeed_forward, TransportModes = transportModes };
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
        
        public IEnumerable<Edge> GetEdges()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Node> GetInternalNodes()
        {
            return null;
        }

        public IEnumerable<Node> GetNodes()
        {
            return nodesArray;
        }

        public async Task InitController()
        {
            await DBLoadAsync();
            Clean();
        }

        public void Clean(){
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            logger.Info("Graph cleaning");
            foreach (var n in nodesArray)
            {
                n.ValidSource = false;
                n.ValidTarget = false;
            }
            var toProcess = new Queue<Node>();
            var root = nodesArray.First();
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
        }
    }