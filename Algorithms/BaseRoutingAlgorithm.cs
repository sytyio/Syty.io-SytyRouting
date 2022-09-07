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
            xymSourcePoint.M = initialTimeStamp.TotalMilliseconds;// 0;

            Console.WriteLine("                             < Node {0}", nodeRoute[0].OsmID);
            Console.WriteLine("S({0}, {1}, {2})", xymSourcePoint.X, xymSourcePoint.Y, xymSourcePoint.M);

            xymRoute.Add(xymSourcePoint);

            for(var i = 0; i < nodeRoute.Count-1; i++)
            {                
                var edge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);
                if(edge is not null)
                {
                    Console.WriteLine("                             < Node {0} (max. speed {1} [km/h])", nodeRoute[i].OsmID, edge.MaxSpeedMPerS * 60 * 60 / 1000);
                    var minTimeIntervalMilliseconds = edge.LengthM / edge.MaxSpeedMPerS * 1000; // [s]*[1000 ms / 1s]
                    if(edge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < edge.InternalGeometry.Length; j++)
                        {
                            XYMPoint xymInternalPoint;
                            xymInternalPoint.X = edge.InternalGeometry[j].X;
                            xymInternalPoint.Y = edge.InternalGeometry[j].Y;
                            xymInternalPoint.M = edge.InternalGeometry[j].M * minTimeIntervalMilliseconds;

                            xymRoute.Add(xymInternalPoint);

                            Console.WriteLine("{0}({1}, {2}, {3})", j, xymInternalPoint.X, xymInternalPoint.Y, xymInternalPoint.M);
                        }
                    }

                    XYMPoint xymTargetPoint;
                    xymTargetPoint.X = edge.TargetNode.X;
                    xymTargetPoint.Y = edge.TargetNode.Y;
                    xymTargetPoint.M = minTimeIntervalMilliseconds; // 1; // edge.LengthM;

                    xymRoute.Add(xymTargetPoint);

                    Console.WriteLine("T({0}, {1}, {2})", xymTargetPoint.X, xymTargetPoint.Y, xymTargetPoint.M);
                }
                else
                {
                    throw new Exception("Impossible to find the corresponding Outward Edge");
                }
            }

            XYMPoint[] spaceTimeRoute = xymRoute.ToArray();
            CalculateCumulativeMOrdinate(spaceTimeRoute, spaceTimeRoute.Length - 1);

            Console.WriteLine("> Final space-time route:");
            foreach(var p in spaceTimeRoute)
            {
                Console.WriteLine("P({0}, {1}, {2})", p.X, p.Y, p.M);
            }

            return spaceTimeRoute.ToList();
        }

        private double CalculateCumulativeMOrdinate(XYMPoint[] xymPointSequence, int index)
        {
            while(index > 0 && index < xymPointSequence.Length)
            {
                var cumulativeM = xymPointSequence[index].M + CalculateCumulativeMOrdinate(xymPointSequence, index-1);

                xymPointSequence[index].M = cumulativeM;

                return cumulativeM;
            }
            return xymPointSequence[0].M;
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