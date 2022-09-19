using System.Diagnostics;
using NetTopologySuite.Geometries;
using NLog;
using SytyRouting.Algorithms;
using SytyRouting.Model;

namespace SytyRouting
{
    public class TestBench
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();   

        public static void TestLineStringConversion<T>(Graph graph) where T: IRoutingAlgorithm, new()
        {
            var lineStringRoute = TestConvertRouteFromNodesToLineString<T>(graph);
            TraceLineStringRoute(lineStringRoute);
        }

        public static void TraceLineStringRoute(LineString lineStringRoute)
        {
            logger.Debug("Route: {0} points", lineStringRoute.Count);
            for(var i=0; i<lineStringRoute.Count; i++)
            {
                logger.Debug("{0}: ({1}, {2}, {3})", i, lineStringRoute.Coordinates[i].X, lineStringRoute.Coordinates[i].Y, lineStringRoute.Coordinates[i].M);
            }
        }  

        public static LineString TestConvertRouteFromNodesToLineString<T>(Graph graph) where T: IRoutingAlgorithm, new()
        {
            var routingAlgorithm = new T();
            routingAlgorithm.Initialize(graph);

            var referenceM = new[] {0, 1.25, 2.5, 3.75, 5.0, 5.75, 6.5, 7.25, 8.0, 8.5, 9.0, 9.5, 10.0, 11.75, 13.5, 15.25, 17};
            var testRoute = new List<Node>(0);
            for(var i=0; i<5; i++)
            {
                var node = new Node {Idx=i,OsmID=i,X=i,Y=i};
                testRoute.Add(node);
            }

            int[] lengths = new int[] {5,3,2,7};
            for(var i=0; i<testRoute.Count-1; i++)
            {
                var edge = new Edge {OsmID=i, LengthM=lengths[i], MaxSpeedMPerS=1000.0, SourceNode=testRoute[i], TargetNode=testRoute[i+1]};
                var internalGeometry = new XYMPoint[] {new XYMPoint{M=0.25}, new XYMPoint{M=0.5}, new XYMPoint{M=0.75}};
                edge.InternalGeometry = internalGeometry;
                testRoute[i].OutwardEdges.Add(edge);
            }

            var lineStringRoute = routingAlgorithm.ConvertRouteFromNodesToLineString(testRoute, TimeSpan.Zero);

            logger.Debug("Test Route comparison of M ordinates: Reference :: ComputedRoute");
            var lineStringRouteCoordinates = lineStringRoute.Coordinates;
            if(lineStringRouteCoordinates.Length != referenceM.Length)
            {
                logger.Debug("Inconsistent number of elements");
                return lineStringRoute;
            }
            for(var i = 0; i < lineStringRouteCoordinates.Length; i++)
            {
                var comparisonMark = "";
                if(referenceM[i] != lineStringRouteCoordinates[i].M)
                    comparisonMark = "<<<===";
                logger.Debug("({0,2}): {1,6}::{2,-6} {3}", i, referenceM[i], lineStringRouteCoordinates[i].M, comparisonMark);
            }

            return lineStringRoute;
        }

        
    }
}