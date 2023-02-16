using System.Diagnostics.CodeAnalysis;
using NLog;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public abstract class BaseRoutingAlgorithm : IRoutingAlgorithm
    {
        [NotNull]
        protected Graph? _graph = null!;
        protected List<Node> route = new List<Node>();
        protected Dictionary<int, Tuple<byte,int>> transportModeTransitions = new Dictionary<int, Tuple<byte,int>>(1);
        public List<Tuple<string,DateTime>> transitions = new List<Tuple<string,DateTime>>(2);
        protected double routeCost;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        //DEBUG:
        protected int Steps = 0;
        protected int Scores = 0;

        public virtual void Initialize(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(double x1, double y1, double x2, double y2, byte[] transportModesSequence)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }

            var originNode = _graph.GetNodeByLongitudeLatitude(x1, y1, isSource: true);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(x2, y2, isTarget: true);

            if(originNode == destinationNode)
            {
                logger.Debug("Origin and destination nodes are equal. Skipping route calculation.");
                return null!;
            }
            
            return RouteSearch(originNode, destinationNode, transportModesSequence);
        }

        public List<Node> GetRoute(Node origin, Node destination, byte[] transportModesSequence)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
            
            return RouteSearch(origin, destination, transportModesSequence);
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId, byte[] transportModesSequence)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
            Node originNode;
            try
            {
                originNode = _graph.GetNodeByOsmId(originNodeOsmId);
            }
            catch (ArgumentException)
            {
                throw new Exception("Unknown value for node osm id.");
            }

            Node destinationNode;
            try
            {
                destinationNode = _graph.GetNodeByOsmId(destinationNodeOsmId);
            }
            catch (ArgumentException)
            {
                throw new Exception("Unknown value for node osm id.");
            }

            return RouteSearch(originNode, destinationNode, transportModesSequence);
        }

        public LineString NodeRouteToLineStringMSeconds(double startX, double startY, double endX, double endY, List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
            transitions.Clear();

            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);

            var numberOfNodes = nodeRoute.Count;

            if(numberOfNodes <= 1)
            {
                return new LineString(null, geometryFactory);
            }
            
            List<Coordinate> xyCoordinates = new List<Coordinate>(numberOfNodes+2); // number of nodes +1 start point (home) +1 end point (work)
            List<double> mOrdinates = new List<double>(numberOfNodes+2);

            var previousTimeInterval = initialTimeStamp.TotalSeconds;

            byte inboundMode = TransportModes.None;
            byte outboundMode = TransportModes.DefaultMode; // Assuming the user starts the journey as a 'Pedestrian'
            
            xyCoordinates.Add(new Coordinate(startX,startY));
            mOrdinates.Add(previousTimeInterval);

            AddTransition(outboundMode,previousTimeInterval);

            var firstNodeX = nodeRoute.First().X;
            var firstNodeY = nodeRoute.First().Y;

            previousTimeInterval = Get2PointTimeInterval(startX,startY,firstNodeX,firstNodeY,outboundMode);

            xyCoordinates.Add(new Coordinate(firstNodeX,firstNodeY));
            mOrdinates.Add(previousTimeInterval);

            for(var i = 0; i < nodeRoute.Count-1; i++)
            {
                inboundMode = outboundMode;

                var newOutboundMode = SelectTransportMode(nodeRoute[i].Idx, transportModeTransitions);

                if(newOutboundMode!=TransportModes.None && newOutboundMode!=outboundMode)
                {
                    outboundMode = newOutboundMode;
                    AddTransition(outboundMode,previousTimeInterval);
                }

                var outboundEdge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);

                if(outboundEdge is not null)
                {
                    var speed = Helper.ComputeSpeed(outboundEdge,outboundMode);
                    
                    //var minTimeIntervalS = edge.LengthM / edge.MaxSpeedMPerS; // [s]
                    var minTimeInterval = outboundEdge.LengthM / speed; // [s]

                    minTimeInterval = Helper.ApplyRoutingPenalties(outboundEdge,outboundMode, minTimeInterval);

                    if(outboundEdge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < outboundEdge.InternalGeometry.Length; j++)
                        {
                            var internalX = outboundEdge.InternalGeometry[j].X;
                            var internalY = outboundEdge.InternalGeometry[j].Y;
                            var internalM = outboundEdge.InternalGeometry[j].M * minTimeInterval + previousTimeInterval;

                            xyCoordinates.Add(new Coordinate(internalX, internalY));
                            mOrdinates.Add(internalM);
                        }
                    }

                    var targetX = outboundEdge.TargetNode.X;
                    var targetY = outboundEdge.TargetNode.Y;

                    previousTimeInterval = minTimeInterval + previousTimeInterval;

                    xyCoordinates.Add(new Coordinate(targetX, targetY));
                    mOrdinates.Add(previousTimeInterval);
                }
                else
                {
                    return new LineString(null, geometryFactory);
                }
            }

            var lastNodeX = nodeRoute.Last().X;
            var lastNodeY = nodeRoute.Last().Y;
            var endM = Get2PointTimeInterval(lastNodeX,lastNodeY,endX,endY,TransportModes.DefaultMode) + previousTimeInterval;
            
            xyCoordinates.Add(new Coordinate(endX,endY));
            mOrdinates.Add(endM);

            AddTransition(TransportModes.DefaultMode,endM);

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, geometryFactory);
        }

        private Edge FindEdge(Node baseNode, Node targetNode, byte transportMode)
        {
            Edge minLengthEdge = null!; // = baseNode.OutwardEdges.Find(e => e.TargetNode.Idx == targetNode.Idx);

            var edges = baseNode.GetOutboundEdges(transportMode);
            if(edges.Count > 0)
            {
                double minLength = Double.PositiveInfinity;
                foreach(var edge in edges)
                {
                    if(edge.TargetNode.Idx == targetNode.Idx && minLength > edge.LengthM)
                    {
                        minLength = edge.LengthM;
                        minLengthEdge = edge;
                    }
                }
            }
            else
            {
                logger.Debug("Edges not found. Source node: {0}, target node: {1}, transport mode: {2}",baseNode.Idx,targetNode.Idx,TransportModes.MaskToString(transportMode));
                _graph.TraceOneNode(baseNode);
            }

            if(minLengthEdge == null)
            {
                logger.Debug("Edge not found");
            }

            return minLengthEdge;
        }

        public LineString TwoPointLineString(double x1, double y1, double x2, double y2, byte transportMode, TimeSpan initialTimeStamp)
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);
            
            List<Coordinate> xyCoordinates = new List<Coordinate>(2);
            List<double> mOrdinates = new List<double>(2);

            var sourcePointX = x1;
            var sourcePointY = y1;
            var previousTimeIntervalS = initialTimeStamp.TotalSeconds;

            xyCoordinates.Add(new Coordinate(x1,y1));
            mOrdinates.Add(previousTimeIntervalS);

            double minTimeIntervalS = Get2PointTimeInterval(x1,y1,x2,y2,transportMode);

            previousTimeIntervalS = minTimeIntervalS + previousTimeIntervalS;

            xyCoordinates.Add(new Coordinate(x2, y2));
            mOrdinates.Add(previousTimeIntervalS);

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, geometryFactory);
        }

        private void AddTransition(byte transportMode, double mOrdinate)
        {
            DateTime timeStamp = Constants.BaseDateTime.Add(TimeSpan.FromSeconds(mOrdinate));
            String transportModeName = TransportModes.SingleMaskToString(transportMode);
            var transition = new Tuple<string,DateTime>(transportModeName,timeStamp);
            transitions.Add(transition);
        }

        private double Get2PointTimeInterval(double x1, double y1, double x2, double y2, byte transportMode)
        {
            double timeInterval = 0;
            var distance = Helper.GetDistance(x1,y1,x2,y2);
            
            if(distance == 0)
            {
                logger.Debug("Distance between Origin and Destination points is zero.");
            }

            if(TransportModes.MasksToSpeeds.ContainsKey(transportMode))
            {
                timeInterval = distance / TransportModes.MasksToSpeeds[transportMode];

                if(timeInterval>0)
                {
                    return timeInterval;
                }
                else
                {
                    logger.Debug("Time interval between Origin and Destination points is zero.");
                }
            }
            else
            {
                logger.Debug("Requested transport mode '{0}' not found.", TransportModes.SingleMaskToString(transportMode));
            }

            throw new Exception("Unable to calculate the requested time interval.");
        }

        public Dictionary<int, Tuple<byte,int>> SingleTransportModeTransition(Node origin, Node destination, byte transportMode)
        {
            Dictionary<int, Tuple<byte,int>> transportModeTransitions = new Dictionary<int, Tuple<byte,int>>(2);
            var transition = Tuple.Create<byte,int>(transportMode,-1);
            transportModeTransitions.Add(origin.Idx,transition);
            if(origin==destination)
                transportModeTransitions.Add(-1,transition);
            else
                transportModeTransitions.Add(destination.Idx,transition);

            return transportModeTransitions;
        }

        private byte SelectTransportMode(int nodeIdx, Dictionary<int,Tuple<byte,int>> transitions)
        {
            byte transportMode = TransportModes.None;

            if(transitions.ContainsKey(nodeIdx))
            {
                var routeType = transitions[nodeIdx].Item2;
                if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportMode = TransportModes.TagIdToTransportModes(routeType);
                else
                    transportMode = transitions[nodeIdx].Item1;
            }           

            return transportMode;
        }

        public double GetRouteCost()
        {
            return routeCost;
        }

        public Tuple<string[],DateTime[]> GetTransportModeTransitions()
        {
            string[] transportModes = new string[transitions.Count];
            DateTime[] timeStamps = new DateTime[transitions.Count];

            for(int i = 0; i < transitions.Count; i++)
            {
                transportModes[i]=transitions[i].Item1;
                timeStamps[i]=transitions[i].Item2;
            }

            return Tuple.Create<string[],DateTime[]>(transportModes,timeStamps);
        }

        // Routing algorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination, byte[] transportModesSequence)
        {
            throw new NotImplementedException();
        }
    }
}