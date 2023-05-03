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
        private List<Tuple<string,DateTime>> _transitions = new List<Tuple<string,DateTime>>(2);
        protected double routeCost;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private DotSpatialAffineCoordinateSequenceFactory _sequenceFactory = null!;
        private GeometryFactory _geometryFactory = null!;
        private LineString _emptyLineString = null!;
        private Tuple<string[],DateTime[]> _emptyTTextTransition = null!;

    
        public virtual void Initialize(Graph graph)
        {
            _graph = graph;
            _sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            _geometryFactory = new GeometryFactory(_sequenceFactory);
            _emptyLineString = new LineString(null, _geometryFactory);

            string[] _noTransportMode = new string[1] {TransportModes.NoTransportMode};
            DateTime[] _baseDateTime = new DateTime[1] {DateTime.UtcNow};
            _emptyTTextTransition = new Tuple<string[],DateTime[]>(_noTransportMode,_baseDateTime);
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

        public LineString NodeRouteToLineStringMSeconds(double startX, double startY, double endX, double endY, List<Node> nodeRoute, TimeSpan initialTimeStamp, DateTime startTime)
        {
            _transitions.Clear();

            var numberOfNodes = nodeRoute.Count;

            if(numberOfNodes <= 1)
            {
                return _emptyLineString;
            }
            
            List<Coordinate> xyCoordinates = new List<Coordinate>(numberOfNodes+2); // number of nodes +1 start point (home) +1 end point (work)
            List<double> mOrdinates = new List<double>(numberOfNodes+2);

            var previousTimeInterval = initialTimeStamp.TotalSeconds;

            byte inboundMode = TransportModes.DefaultMode;
            byte outboundMode = TransportModes.DefaultMode; // Assuming the user starts the journey as a 'Pedestrian'
            
            xyCoordinates.Add(new Coordinate(startX,startY));
            mOrdinates.Add(previousTimeInterval);

            AddTransition(outboundMode,previousTimeInterval,startTime);

            var firstNodeX = nodeRoute.First().X;
            var firstNodeY = nodeRoute.First().Y;

            previousTimeInterval = Get2PointTimeInterval(startX,startY,firstNodeX,firstNodeY,outboundMode);
            if(previousTimeInterval==0)
            {
                return _emptyLineString;
            }

            xyCoordinates.Add(new Coordinate(firstNodeX,firstNodeY));
            mOrdinates.Add(previousTimeInterval);

            for(var i = 0; i < nodeRoute.Count-1; i++)
            {
                var newOutboundMode = SelectTransportMode(nodeRoute[i].Idx, transportModeTransitions);

                if(newOutboundMode!=TransportModes.None && newOutboundMode!=inboundMode)
                {
                    outboundMode = newOutboundMode;
                    AddTransition(outboundMode,previousTimeInterval,startTime);
                }

                var outboundEdge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);

                if(outboundEdge is not null)
                {
                    var speed = Helper.ComputeSpeed(outboundEdge,outboundMode);
                    
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

                    inboundMode = outboundMode;
                }
                else
                {
                    return _emptyLineString;
                }
            }

            var lastNodeX = nodeRoute.Last().X;
            var lastNodeY = nodeRoute.Last().Y;
            
            outboundMode = TransportModes.DefaultMode;
            if(outboundMode!=inboundMode)
            {
                AddTransition(outboundMode,previousTimeInterval,startTime);
            }
            
            var endM = Get2PointTimeInterval(lastNodeX,lastNodeY,endX,endY,TransportModes.DefaultMode) + previousTimeInterval;
            if(endM==0)
            {
                return _emptyLineString;
            }
            
            xyCoordinates.Add(new Coordinate(endX,endY));
            mOrdinates.Add(endM);

            AddTransition(TransportModes.DefaultMode,endM,startTime);

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, _geometryFactory);
        }

        public LineString TwoPointLineString(double x1, double y1, double x2, double y2, byte transportMode, TimeSpan initialTimeStamp)
        {   
            List<Coordinate> xyCoordinates = new List<Coordinate>(2);
            List<double> mOrdinates = new List<double>(2);

            var sourcePointX = x1;
            var sourcePointY = y1;
            var previousTimeIntervalS = initialTimeStamp.TotalSeconds;

            xyCoordinates.Add(new Coordinate(x1,y1));
            mOrdinates.Add(previousTimeIntervalS);

            double minTimeIntervalS = Get2PointTimeInterval(x1,y1,x2,y2,transportMode);
            if(minTimeIntervalS==0)
            {
                return _emptyLineString;
            }

            previousTimeIntervalS = minTimeIntervalS + previousTimeIntervalS;

            xyCoordinates.Add(new Coordinate(x2, y2));
            mOrdinates.Add(previousTimeIntervalS);

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, _geometryFactory);
        }

        private void AddTransition(byte transportMode, double mOrdinate, DateTime startTime)
        {
            DateTime timeStamp = startTime.Add(TimeSpan.FromSeconds(mOrdinate));
            String transportModeName = TransportModes.SingleMaskToString(transportMode);
            var transition = new Tuple<string,DateTime>(transportModeName,timeStamp);
            _transitions.Add(transition);
        }

        private double Get2PointTimeInterval(double x1, double y1, double x2, double y2, byte transportMode)
        {
            double timeInterval = 0.0;
            var distance = Helper.GetDistance(x1,y1,x2,y2);
            
            if(distance == 0)
            {
                logger.Debug("Distance between Origin and Destination points is zero.");
                return timeInterval;
            }

            if(TransportModes.MasksToSpeeds.ContainsKey(transportMode))
            {
                timeInterval = distance / TransportModes.MasksToSpeeds[transportMode];

                if(timeInterval<=0)
                {
                    logger.Debug("Invalid time interval between Origin and Destination points: {0}",timeInterval);
                }
            }
            else
            {
                logger.Debug("Requested transport mode '{0}' not found.", TransportModes.SingleMaskToString(transportMode));
            }

            return timeInterval;
        }

        public Tuple<string[],DateTime[]> SingleTransportModeTransition(Persona persona, Node origin, Node destination, byte transportMode)
        {
            Dictionary<int, Tuple<byte,int>> transportModeTransitions = new Dictionary<int, Tuple<byte,int>>(2);
            var transition = Tuple.Create<byte,int>(transportMode,-1);
            transportModeTransitions.Add(origin.Idx,transition);
            if(origin==destination)
                transportModeTransitions.Add(-1,transition);
            else
                transportModeTransitions.Add(destination.Idx,transition);

            return SingleTransportTransitionsToTTEXTSequence(persona, transportModeTransitions);
        }

        private Tuple<string[],DateTime[]> SingleTransportTransitionsToTTEXTSequence(Persona persona, Dictionary<int,Tuple<byte,int>> transitions)
        {
            var route = persona.Route;
            if(route == null || transitions == null || transitions.Count <1 || route.IsEmpty)
                return _emptyTTextTransition;

            var startTime = persona.StartDateTime;
            var coordinates = route.Coordinates;

            List<DateTime> timeStamps = new List<DateTime>(transitions.Count);
            List<string> transportModes = new List<string>(transitions.Count);

            Node origin = _graph.GetNodeByLongitudeLatitude(coordinates[0].X, coordinates[0].Y);
        
            string transportModeS = "";
                        
            byte currentTransportMode = 0;    

            if(transitions.ContainsKey(origin.Idx))
            {
                currentTransportMode = transitions[origin.Idx].Item1;
                var routeType = transitions[origin.Idx].Item2;
                if(routeType==-1)
                    transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                else if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
                timeStamps.Add(startTime.Add(TimeSpan.FromSeconds(route.Coordinates[0].M))); //DEBUG: CHECK UNITS!
                transportModes.Add(transportModeS);                    
            }
            
            Node destination = _graph.GetNodeByLongitudeLatitude(coordinates[route.Count -1].X, coordinates[route.Count -1].Y);

            timeStamps.Add(startTime.Add(TimeSpan.FromSeconds(route.Coordinates[route.Count -1].M))); //DEBUG: CHECK UNITS!

            if(transitions.ContainsKey(destination.Idx))
            {
                var routeType = transitions[destination.Idx].Item2;
                if(routeType==-1)
                    transportModeS = TransportModes.SingleMaskToString(currentTransportMode);
                else if(!TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType))
                    transportModeS = TransportModes.SingleMaskToString(TransportModes.TagIdToTransportModes(routeType));
            }
            transportModes.Add(transportModeS);

            return new Tuple<string[],DateTime[]>(transportModes.ToArray(), timeStamps.ToArray());
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
            if(_transitions.Count<2)
            {
                logger.Debug("Invalid transport transition data.");
                return _emptyTTextTransition;
            }

            string[] transportModes = new string[_transitions.Count];
            DateTime[] timeStamps = new DateTime[_transitions.Count];

            for(int i = 0; i < _transitions.Count; i++)
            {
                transportModes[i]=_transitions[i].Item1;
                timeStamps[i]=_transitions[i].Item2;
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