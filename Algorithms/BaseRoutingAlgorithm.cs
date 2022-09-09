using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries;
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

        public List<Node> GetRoute(double x1, double y1, double x2, double y2)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
            var originNode = _graph.GetNodeByLongitudeLatitude(x1, y1);
            var destinationNode = _graph.GetNodeByLongitudeLatitude(x2, y2);
            
            return RouteSearch(originNode, destinationNode);
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
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

            return RouteSearch(originNode, destinationNode);
        }

        public List<XYMPoint> ConvertRouteFromNodesToXYMPoints(List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
            List<XYMPoint> xymRoute = new List<XYMPoint>(0);

            XYMPoint xymSourcePoint;
            xymSourcePoint.X = nodeRoute[0].X;
            xymSourcePoint.Y = nodeRoute[0].Y;
            xymSourcePoint.M = initialTimeStamp.TotalMilliseconds;
            var previousTimeInterval = xymSourcePoint.M;

            xymRoute.Add(xymSourcePoint);

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
                            XYMPoint xymInternalPoint;
                            xymInternalPoint.X = edge.InternalGeometry[j].X;
                            xymInternalPoint.Y = edge.InternalGeometry[j].Y;
                            xymInternalPoint.M = edge.InternalGeometry[j].M * minTimeIntervalMilliseconds + previousTimeInterval;

                            xymRoute.Add(xymInternalPoint);
                        }
                    }

                    XYMPoint xymTargetPoint;
                    xymTargetPoint.X = edge.TargetNode.X;
                    xymTargetPoint.Y = edge.TargetNode.Y;
                    xymTargetPoint.M = minTimeIntervalMilliseconds + previousTimeInterval;
                    previousTimeInterval = xymTargetPoint.M;

                    xymRoute.Add(xymTargetPoint);
                }
                else
                {
                    throw new Exception("Impossible to find the corresponding Outward Edge");
                }
            }

            return xymRoute;
        }

        // private LineString CreateLineStringRoute(List<XYMPoint> xymPointRoute)
        // {
        //     CoordinateSequence coordinateSequence = new CoordinateSequence(0,3,1);
        //     // foreach(var xymPoint in xymPointRoute)
        //     // {
        //     //     Point point = new Point(xymPoint.X, )
        //     // }
            
        // }

        public double GetRouteCost()
        {
            return routeCost;
        }

        // Routing algorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination)
        {
            throw new NotImplementedException();
        }
    }
}