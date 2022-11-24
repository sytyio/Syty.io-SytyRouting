using System.Diagnostics.CodeAnalysis;
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
        protected Dictionary<int, byte> transportModeTransitions = new Dictionary<int, byte>(1);
        protected double routeCost;

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
            var originNode = _graph.GetNodeByLongitudeLatitude(x1, y1);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(x2, y2);
            
            return RouteSearch(originNode, destinationNode, transportModesSequence);
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

        public LineString ConvertRouteFromNodesToLineString(List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);

            if(nodeRoute.Count <= 1)
            {
                return new LineString(null, geometryFactory);
            }
            
            List<Coordinate> xyCoordinates = new List<Coordinate>(0);
            List<double> mOrdinates = new List<double>(0);

            var sourcePointX = nodeRoute[0].X;
            var sourcePointY = nodeRoute[0].Y;
            var previousTimeInterval = initialTimeStamp.TotalMilliseconds;

            xyCoordinates.Add(new Coordinate(sourcePointX, sourcePointY));
            mOrdinates.Add(previousTimeInterval);

            for(var i = 0; i < nodeRoute.Count-1; i++)
            {          
                var edge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);
                if(edge is not null)
                {
                    if(edge.MaxSpeedMPerS==0)
                    {
                        return new LineString(null, geometryFactory);
                    }
                    
                    var minTimeIntervalMilliseconds = edge.LengthM / edge.MaxSpeedMPerS * 1000; // [s]*[1000 ms / 1s]

                    if(edge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < edge.InternalGeometry.Length; j++)
                        {
                            var internalPointX = edge.InternalGeometry[j].X;
                            var internalPointY = edge.InternalGeometry[j].Y;
                            var internalPointM = edge.InternalGeometry[j].M * minTimeIntervalMilliseconds + previousTimeInterval;

                            xyCoordinates.Add(new Coordinate(internalPointX, internalPointY));
                            mOrdinates.Add(internalPointM);
                        }
                    }

                    var targetPointX = edge.TargetNode.X;
                    var targetPointY = edge.TargetNode.Y;
                    previousTimeInterval = minTimeIntervalMilliseconds + previousTimeInterval;

                    xyCoordinates.Add(new Coordinate(targetPointX, targetPointY));
                    mOrdinates.Add(previousTimeInterval);
                }
                else
                {
                    return new LineString(null, geometryFactory);
                }
            }

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();

            return new LineString(coordinateSequence, geometryFactory);
        }

        public double GetRouteCost()
        {
            return routeCost;
        }

        //DEBUG:
        public int GetSteps()
        {
            return Steps;
        }

        //DEBUG:
        public int GetScores()
        {
            return Scores;
        }

        public Dictionary<int,byte> GetTransportModeTransitions()
        {
            Dictionary<int,byte> tmTransitions =  new Dictionary<int, byte>(transportModeTransitions.Count);
            var tmTransitionsKeys = transportModeTransitions.Keys.ToArray();
            var tmTransitionsValues = transportModeTransitions.Values.ToArray();

            for(int i = 0; i < tmTransitionsKeys.Length; i++)
            {
                tmTransitions.Add(tmTransitionsKeys[i],tmTransitionsValues[i]);
            }

            return tmTransitions;
        }

        // Routing algorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination, byte[] transportModesSequence)
        {
            throw new NotImplementedException();
        }
    }
}