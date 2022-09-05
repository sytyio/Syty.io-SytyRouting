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

        public List<Edge> ConvertRouteFromNodesToEdges(List<Node> route)
        {
            List<Edge> edgeRoute = new List<Edge>(0);
            List<XYMPoint> geometryRoute = new List<XYMPoint>(0);

            for(var i = 0; i < route.Count-1; i++)
            {
                Console.WriteLine("> Node {0} :: ", route[i].OsmID);
                double lastM = 0;
                var edge = route[i].OutwardEdges.Find(e => e.TargetNode.Idx == route[i+1].Idx);
                if(edge is not null)
                {
                    edgeRoute.Add(edge);

                    XYMPoint xymSourcePoint;
                    xymSourcePoint.X = edge.SourceNode.X;
                    xymSourcePoint.Y = edge.SourceNode.Y;
                    xymSourcePoint.M = 0;

                    geometryRoute.Add(xymSourcePoint);

                    Console.WriteLine("S({0}, {1}, {2})", xymSourcePoint.X, xymSourcePoint.Y, xymSourcePoint.M);

                    if(edge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < edge.InternalGeometry.Length; j++)
                        {
                            XYMPoint xymInternalPoint;
                            xymInternalPoint.X = edge.InternalGeometry[j].X;
                            xymInternalPoint.Y = edge.InternalGeometry[j].Y;
                            xymInternalPoint.M = edge.InternalGeometry[j].M;

                            lastM = xymInternalPoint.M;

                            geometryRoute.Add(xymInternalPoint);

                            Console.WriteLine("{0}({1}, {2}, {3})", j, xymInternalPoint.X, xymInternalPoint.Y, xymInternalPoint.M);
                        }
                    }

                    XYMPoint xymTargetPoint;
                    xymTargetPoint.X = edge.TargetNode.X;
                    xymTargetPoint.Y = edge.TargetNode.Y;
                    xymTargetPoint.M = edge.LengthM;

                    geometryRoute.Add(xymTargetPoint);

                    var mDifference = lastM - xymTargetPoint.M;
                    if(mDifference>0)
                        throw new Exception("Inconsistent distance");
                    Console.WriteLine("T({0}, {1}, {2})", xymTargetPoint.X, xymTargetPoint.Y, xymTargetPoint.M);
                    Console.WriteLine(" :: M-difference: {0} - {1} = {2}", lastM, xymTargetPoint.M, mDifference);
                }
                else
                {
                    throw new Exception("Impossible to find the corresponding Outward Edge");
                }
            }

            return edgeRoute;
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