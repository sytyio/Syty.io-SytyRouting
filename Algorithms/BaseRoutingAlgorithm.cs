using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public abstract class BaseRoutingAlgorithm : IRoutingAlgorithm
    {
        [NotNull]
        protected Graph? _graph;
        protected List<Node> route = new List<Node>();
        protected double routeCost;

        public virtual void Initialize(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(double x1, double y1, double x2, double y2, int transportMode)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
            var originNode = _graph.GetNodeByLongitudeLatitude(x1, y1);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(x2, y2);
            
            return RouteSearch(originNode, destinationNode, transportMode);
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId, int transportMode)
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

            return RouteSearch(originNode, destinationNode, transportMode);
        }

        public LineString ConvertRouteFromNodesToLineString(List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
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
                    throw new Exception("Impossible to find the corresponding Outward Edge");
                }
            }

            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);
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

        // Routing algorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination, int transportMode)
        {
            throw new NotImplementedException();
        }
    }
}