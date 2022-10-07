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

        // queryString = "SELECT osm_id, source, target, cost, reverse_cost, one_way, x1, y1, x2, y2, source_osm, target_osm, length_m, the_geom, maxspeed_forward, maxspeed_backward, length, ST_Length(the_geom) as st_length FROM public.ways where length_m is not null";
        public static int NumberOfLengthSTLengthDiscrepancies {get; set;} = 0;
        public static int NumberOfCostStCostDiscrepancies {get; set;} = 0;
        public static int NumberOfReverseCostStReverseCostDiscrepancies {get; set;} = 0;
        public static void TestOriginalWayCostCalculation(double length, double stLength, double edgeCost, double edgeReverseCost, OneWayState edgeOneWay)
        {
            // cost based on ST_Length(the_geom)
            double stCost = double.NaN;
            double stReverseCost = double.NaN;
            double diffLengthSTLength = length - stLength;
            if(diffLengthSTLength != 0.0)
            {
                logger.Debug("length {0}:{1} st_length :: diff {2}", length, stLength, diffLengthSTLength);
                NumberOfLengthSTLengthDiscrepancies++;
            }
                
            double diffCostStCost = double.NaN;
            double diffReverseCostStReverseCost = double.NaN;
            switch(edgeOneWay)
            {
                case OneWayState.Reversed:
                {
                    stCost = -stLength;
                    stReverseCost = stLength;
                    break; 
                }
                case OneWayState.Yes:
                {
                    stCost = stLength;
                    stReverseCost = -stLength;
                    break; 
                }
                default:
                {
                    stCost = stLength;
                    stReverseCost = stLength;
                    break; 
                }
            }

            diffCostStCost = edgeCost - stCost;
            if(diffCostStCost != 0.0)
            {
                logger.Debug("cost {0}:{1} st_cost :: diff = {2}, one_way: {3}", length, edgeCost, diffCostStCost, edgeOneWay);
                NumberOfCostStCostDiscrepancies++;
            }
        
            diffReverseCostStReverseCost = edgeReverseCost - stReverseCost;
            if(diffReverseCostStReverseCost != 0.0)
            {
                logger.Debug("reverse_cost {0}:{1} st_reverse_cost :: diff = {2}, one_way: {3}", length, edgeReverseCost, diffReverseCostStReverseCost, edgeOneWay);
                NumberOfReverseCostStReverseCostDiscrepancies++;
            }
        }

        public static int NumberOfSTLengthTheGeomLengthDiscrepancies {get; set;} = 0;
        public static void TestOriginalGeomLengthCalculation(double length, double stLength, Geometry theGeom)
        {
            // length calculation:
            double theGeomLength = theGeom.Length;
            double diffStLengthTheGeomLength = stLength-theGeomLength;

            if(diffStLengthTheGeomLength !=0)
            {
                logger.Debug("       length: {0}", length);
                logger.Debug("    st_length: {0}", stLength);
                logger.Debug("   difference: {0}\n", length-stLength);
                logger.Debug("    st_length: {0}", stLength);
                logger.Debug("theGeomlength: {0}", theGeomLength);
                logger.Debug("   difference: {0}\n", diffStLengthTheGeomLength);
                NumberOfSTLengthTheGeomLengthDiscrepancies++;
            }
        }

        public static void DisplayCostCalculationTestResults()
        {
            logger.Debug("\n\n");
            logger.Debug("NumberOfLengthSTLengthDiscrepancies: {0}", TestBench.NumberOfLengthSTLengthDiscrepancies);
            logger.Debug("NumberOfCostLengthDiscrepancies: {0}", TestBench.NumberOfCostStCostDiscrepancies);
            logger.Debug("NumberOfReverseCostLengthDiscrepancies: {0}", TestBench.NumberOfReverseCostStReverseCostDiscrepancies);
            logger.Debug("\n\n");
            logger.Debug("NumberOfSTLengthTheGeomLengthDiscrepancies: {0}", TestBench.NumberOfSTLengthTheGeomLengthDiscrepancies);
            logger.Debug("\n\n");
        }



        
    }
}