using System.Diagnostics;
using NetTopologySuite.Geometries;
using NLog;
using SytyRouting.Algorithms;
using SytyRouting.Algorithms.Dijkstra;
using SytyRouting.Model;

namespace SytyRouting
{
    public class TestBench
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

            var lineStringRoute = routingAlgorithm.NodeRouteToLineStringMSeconds(0,0,0,0, testRoute, TimeSpan.Zero);

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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static int NumberOfSTLengthTheGeomLengthDiscrepancies {get; set;} = 0;
        public static int NumberOfTheGeomLengthTheGeomLengthCoordinateLengthDiscrepancies {get; set;} = 0;
        public static void TestOriginalGeomLengthCalculation(double length, double stLength, Geometry theGeom)
        {
            // length calculation:
            double theGeomLength = theGeom.Length; // (LineString length)
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

            // double theGeomCoordinateSequenceLength = GeometryLength(theGeom.Coordinates);
            double theGeomCoordinateSequenceLength = GeometryLength(theGeom);
            double diffTheGeomLengthTheGeomCoordinateSequenceLength = theGeomLength-theGeomCoordinateSequenceLength;
            if(diffTheGeomLengthTheGeomCoordinateSequenceLength !=0)
            {
                logger.Debug("                  theGeomLength: {0}", theGeomLength);
                logger.Debug("theGeomCoordinateSequenceLength: {0}", theGeomCoordinateSequenceLength);
                logger.Debug("                     difference: {0}\n", diffStLengthTheGeomLength);
                NumberOfTheGeomLengthTheGeomLengthCoordinateLengthDiscrepancies++;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static int NumberOfLengthMTheGeomLengthMDiscrepancies {get; set;} = 0;
        public static void TestOriginalGeomLengthCalculationMeters(double length_m, double stLengthSpheroid, Geometry theGeom)
        {
            // length calculation:
            //double theGeomLengthM =HaversineDistance(theGeom); // (LineString length in meters)
            double theGeomLengthM =stLengthSpheroid; // (LineString length in meters based on a spheroid)
            double theGeomLengthMHelperGetDistance = GeometryLengthM(theGeom);
            
            double diffLengthMTheGeomLengthM = length_m-theGeomLengthM;
            double diffLengthMTheGeomLengthMHelperGetDistance = length_m-theGeomLengthMHelperGetDistance;
            
            if(diffLengthMTheGeomLengthM != 0)
            {
                logger.Debug("      length_m: {0}", length_m);
                logger.Debug("theGeomlengthM: {0}", theGeomLengthM);
                logger.Debug("   difference: {0}\n", diffLengthMTheGeomLengthM);

                NumberOfLengthMTheGeomLengthMDiscrepancies++;
            }

            // if(diffLengthMTheGeomLengthMHelperGetDistance != 0)
            // {
            //     logger.Debug("                       length_m: {0}", length_m);
            //     logger.Debug("theGeomlengthMHelperGetDistance: {0}", theGeomLengthMHelperGetDistance);
            //     logger.Debug("                     difference: {0}\n", diffLengthMTheGeomLengthMHelperGetDistance);            
                // NumberOfLengthMTheGeomLengthMDiscrepancies++;
            // }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        // From NetTopologySuite/src/NetTopologySuite/Algorithm/Length.cs
        public static double GeometryLength(Coordinate[] coordinates)
        {
            // Cartesian 2D distance
            int n = coordinates.Length;
            if (n <= 1)
                return 0.0;

            double length = 0.0;

            var p = coordinates[0];
            double x0 = p.X;
            double y0 = p.Y;

            for (int i = 1; i < n; i++)
            {
                p = coordinates[i];
                double x1 = p.X;
                double y1 = p.Y;
                double dx = x1 - x0;
                double dy = y1 - y0;

                length += Math.Sqrt(dx * dx + dy * dy);

                x0 = x1;
                y0 = y1;
            }
            return length;
        }

        public static double GeometryLength(Geometry geometry)
        {
            LineString linestring = (LineString)geometry;
            var pts = linestring.CoordinateSequence;

            // optimized for processing CoordinateSequences
            int n = pts.Count;
            if (n <= 1)
                return 0.0;

            double len = 0.0;

            var p = pts.GetCoordinateCopy(0);
            double x0 = p.X;
            double y0 = p.Y;

            for (int i = 1; i < n; i++)
            {
                pts.GetCoordinate(i, p);
                double x1 = p.X;
                double y1 = p.Y;
                double dx = x1 - x0;
                double dy = y1 - y0;

                len += Math.Sqrt(dx * dx + dy * dy);

                x0 = x1;
                y0 = y1;
            }
            return len;
        }

        public static double GeometryLengthM(Geometry geometry)
        {
            LineString linestring = (LineString)geometry;
            var pts = linestring.CoordinateSequence;

            // optimized for processing CoordinateSequences
            int n = pts.Count;
            if (n <= 1)
                return 0.0;

            double len = 0.0;

            var p = pts.GetCoordinateCopy(0);
            double x0 = p.X;
            double y0 = p.Y;

            for (int i = 1; i < n; i++)
            {
                pts.GetCoordinate(i, p);
                double x1 = p.X;
                double y1 = p.Y;
                // double dx = x1 - x0;
                // double dy = y1 - y0;

                //len += Math.Sqrt(dx * dx + dy * dy);
                len += Helper.GetDistance(x0,y0,x1,y1);

                x0 = x1;
                y0 = y1;
            }
            return len;
        }

        public static double ToRadians(double degrees)
        {
            double radians = (Math.PI / 180) * degrees;
            return (radians);
        }

        
        public static double HaversineDistance(Geometry g)
        {
            LineString ls = (LineString)g;

            // optimized for processing CoordinateSequences
            var cs = ls.CoordinateSequence;
            int n = cs.Count;
            if (n <= 1)
                return 0.0;

            double len = 0.0;

            var p = cs.GetCoordinateCopy(0);
            double x0 = p.X;
            double y0 = p.Y;

            for (int i = 1; i < n; i++)
            {
                cs.GetCoordinate(i, p);
                double x1 = p.X;
                double y1 = p.Y;
                double dx = x1 - x0;
                double dy = y1 - y0;

                double R = 6371000;
                var lat = ToRadians(dx);
                var lng = ToRadians(dy);
                var h1 = Math.Sin(lat / 2) * Math.Sin(lat / 2) +
                            Math.Cos(ToRadians(x0)) * Math.Cos(ToRadians(x1)) *
                            Math.Sin(lng / 2) * Math.Sin(lng / 2);
                var h2 = 2 * Math.Asin(Math.Min(1, Math.Sqrt(h1)));
                len += R * h2;

                x0 = x1;
                y0 = y1;
            }
            return len;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static void DisplayCostCalculationTestResults()
        {
            logger.Debug("\n\n");
            if(NumberOfCostStCostDiscrepancies != 0 || NumberOfReverseCostStReverseCostDiscrepancies != 0)
                logger.Debug("> Test of original Ways cost calculation failed.");
            else
                logger.Debug("> Test of original Ways cost calculation succeeded.");
            logger.Debug("NumberOfCostLengthDiscrepancies: {0}", TestBench.NumberOfCostStCostDiscrepancies);
            logger.Debug("NumberOfReverseCostLengthDiscrepancies: {0}", TestBench.NumberOfReverseCostStReverseCostDiscrepancies);
            
            logger.Debug("\n\n");
            logger.Debug("NumberOfLengthSTLengthDiscrepancies: {0}", TestBench.NumberOfLengthSTLengthDiscrepancies);
            logger.Debug("NumberOfSTLengthTheGeomLengthDiscrepancies: {0}", TestBench.NumberOfSTLengthTheGeomLengthDiscrepancies);
            
            logger.Debug("\n\n");
            if(NumberOfSTLengthTheGeomLengthDiscrepancies != 0 || NumberOfTheGeomLengthTheGeomLengthCoordinateLengthDiscrepancies != 0)
                logger.Debug("> Test of the geom vs. the geom coord sequence calculation failed.");
            else
                logger.Debug("> Test of the geom vs. the geom coord sequencelength calculation succeeded.");
            logger.Debug("NumberOfTheGeomLengthTheGeomCoordinateSequenceLengthDiscrepancies: {0}", TestBench.NumberOfTheGeomLengthTheGeomLengthCoordinateLengthDiscrepancies);
            logger.Debug("\n\n");

            if(NumberOfLengthMTheGeomLengthMDiscrepancies != 0)
                logger.Debug("> Test of the length (m) vs. the geom length (m) calculation failed.");
            else
                logger.Debug("> Test of the length (m) vs. the geom length (m) calculation succeeded.");
            logger.Debug("NumberLengthMTheGeomLengthMDiscrepancies: {0}", TestBench.NumberOfLengthMTheGeomLengthMDiscrepancies);
            logger.Debug("\n\n");
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // M ordinate verification (time stamps)

        private static bool isValidSequence(double[] m)
        {
            if(m.Length>0)
            {
                for(int i=1; i<m.Length; i++)
                {
                    if(m[i]<=m[i-1])
                    {
                        Console.WriteLine("M sequence inconsistency");
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public static void DisplayEdgesWithLessThanXMLength(Edge edge,double xm)
        {
            if(edge.LengthM==xm && edge.TagIdRouteType == 13)
            {
                Console.WriteLine("Edge Id: {0} :: LengthM: {1} :: MaxSpeedMPerS: {2} :: TagIdRouteType: {3} :: TransportModes: {4}",edge.OsmID,edge.LengthM,edge.MaxSpeedMPerS,edge.TagIdRouteType,TransportModes.MaskToString(edge.TransportModes));
            }
        }

        public static void SearchForLoopEdges(Graph _graph)
        {
            var nodes = _graph.GetNodes();
            var outwardEdges = nodes.SelectMany(t => t.OutwardEdges).ToArray();
            var inwardEdges  = nodes.SelectMany(t => t.InwardEdges).ToArray();

            List<Edge> _oe = new List<Edge>();
            List<Edge> _ie = new List<Edge>();

            logger.Debug("Nodes: {0}", nodes.Length);
            logger.Debug("Outward Edges: {0}", outwardEdges.Length);
            logger.Debug(" Inward Edges: {0}", inwardEdges.Length);

            for(int n=0; n<nodes.Length; n++)
            {
                foreach(Edge oe in nodes[n].OutwardEdges)
                {
                    _oe.Add(oe);
                }
                foreach(Edge ie in nodes[n].InwardEdges)
                {
                    _ie.Add(ie);
                }
            }
            logger.Debug("Outward Edges: {0}", _oe.Count);
            logger.Debug(" Inward Edges: {0}", _ie.Count);

            int maxFailDisplay=100;
            int fails=0;
            foreach(Edge ie in _ie)
            {
                if(ie.SourceNode == ie.TargetNode)
                {
                    fails++;
                    if(fails<maxFailDisplay)
                    {
                        logger.Debug("Loop edge found:");
                        logger.Debug("Source: {0}::{1} :tegraT", ie.SourceNode.Idx,ie.TargetNode.Idx);
                        _graph.TraceOneNode(ie.SourceNode);
                    }
                }
            }
            logger.Debug("{0} loop edge(s) found", fails);
        }

        public static void TraceNodeToLineStringRouteConversion(List<Node> nodeRoute, int nodeRouteIndex, List<Coordinate> xyCoordinates, List<double> mOrdinates, Edge currentEdge, int currentEdgeInternalGeometryIndex, Edge previousEdge, double minTimeIntervalMilliseconds, double previousTimeIntervalMilliseconds)
        {
            var j = currentEdgeInternalGeometryIndex;
            if(j>=0)
            {
                var internalPointM = currentEdge.InternalGeometry?[j].M * minTimeIntervalMilliseconds + previousTimeIntervalMilliseconds;
                var currentM=internalPointM;
            
                logger.Debug("Current M: {0}", currentM);
                logger.Debug("Current Time Interval: {0}",minTimeIntervalMilliseconds);
                logger.Debug("Previous Time Interval: {0}",previousTimeIntervalMilliseconds);
                logger.Debug("Current Edge OSM Id: {0}",currentEdge.OsmID);
                logger.Debug("Current Edge Source Node OSM Id: {0}",currentEdge.SourceNode.OsmID);
                logger.Debug("Current Edge Target Node OSM Id: {0}",currentEdge.TargetNode.OsmID);
            }

            string notes="";
            if(previousEdge.InternalGeometry!=null)
            {
                int setSize=previousEdge.InternalGeometry.Length+currentEdgeInternalGeometryIndex;
                int n=mOrdinates.Count-setSize-3;
            
                var previousEdgeInternalGeometry=previousEdge.InternalGeometry;
                logger.Debug("index:\t\t\tX-ordinate:\t\t(x-ordinate):\t\tY-ordinate:\t\t(y-Ordinate):\tM-ordinate:\t\tNotes:");
                logger.Debug("{0,5} {1,5}\t{2,18}\t{3,18}\t{4,18}\t{5,18}\t{6,18}\t{7,18}"," "," ",nodeRoute[nodeRouteIndex-1].X,"",nodeRoute[nodeRouteIndex-1].Y,"","","<-   Previous route Node");
                logger.Debug("{0,5} ({1,5})\t{2,18}\t({3,18})\t{4,18}\t({5,18})\t{6,18}\t{7,18}"," ",n,previousEdge.SourceNode.X,xyCoordinates[n].X,previousEdge.SourceNode.Y,xyCoordinates[n].Y,"","<-   Previous Edge Source Node");
                n++;
                for(int m=0; m<previousEdge.InternalGeometry!.Length; m++)
                {
                    if(m==0)
                        notes+=String.Format("<-   Previous Edge Internal Geometry (geometry length={0}), edge type: {1}",previousEdge.InternalGeometry!.Length,previousEdge.TagIdRouteType);
                    else
                        notes="";
                    logger.Debug("{0,5} ({1,5})\t{2,18}\t({3,18})\t{4,18}\t({5,18})\t{6,18}\t{7,18}",m,n,previousEdgeInternalGeometry![m].X,xyCoordinates[n].X,previousEdgeInternalGeometry![m].Y,xyCoordinates[n].Y,previousEdgeInternalGeometry![m].M,notes);
                    n++;
                }
                logger.Debug("{0,5}  {1,5} \t{2,18}\t{3,18}\t{4,18}\t{5,18}\t\t\t\t{6,18}","","",previousEdge.TargetNode.X,"",previousEdge.TargetNode.Y,"","<-   Previous Edge Target Node");
                logger.Debug("---------------------------------------------------------------------------------------------------------------------------------------------------");
                
                var currentEdgeInternalGeometry=currentEdge.InternalGeometry;
                logger.Debug("{0,5} {1,5}\t{2,18}\t{3,18}\t{4,18}\t\t{5,18}\t\t\t{6,18}"," "," ",nodeRoute[nodeRouteIndex].X,"",nodeRoute[nodeRouteIndex].Y,"","<-   Current route Node");
                logger.Debug("{0,5} ({1,5})\t{2,18}\t({3,18})\t{4,18}\t({5,18})\t\t\t\t{6,18}"," ",n,currentEdge.SourceNode.X,xyCoordinates[n].X,currentEdge.SourceNode.Y,xyCoordinates[n].Y,"<-   Current Edge Source Node");
                n++;
                for(int m=0; m<=currentEdgeInternalGeometryIndex; m++)
                {
                    if(m==currentEdgeInternalGeometryIndex)
                        notes=String.Format("<--- M duplication!");
                    else if(m==0)
                        notes+=String.Format("<-   Current Edge Internal Geometry (geometry length={0}), edge type: {1}",currentEdge.InternalGeometry!.Length,currentEdge.TagIdRouteType);
                    else
                        notes="";
                    logger.Debug("{0,5} ({1,5})\t{2,18}\t({3,18})\t{4,18}\t({5,18})\t{6,18}\t{7,18}",m,n,currentEdgeInternalGeometry![m].X,xyCoordinates[n].X,currentEdgeInternalGeometry![m].Y,xyCoordinates[n].Y,currentEdgeInternalGeometry![m].M,notes);
                    n++;
                }
                logger.Debug("{0,5}{1,5}\t\t{2,18}\t{3,18}\t{4,18}\t{5,18}\t\t\t\t{6,18}","","",currentEdge.TargetNode.X,"",currentEdge.TargetNode.Y,"","<-   Current Edge Target Node");
            

            
                logger.Debug("----------------------------------------------------------------------------------------------------------------------------------------------------");
                for(int m=mOrdinates.Count-setSize-1; m<mOrdinates.Count; m++)
                {
                    logger.Debug("{0,5}\t{1,18}\t{2,18}\t{3,18}",m,xyCoordinates[m].X,xyCoordinates[m].Y,mOrdinates[m]);
                }
            }


            //Environment.Exit(0);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Node ValidSource/ValidTarget verification
        
        public static void SearchForNodesBySourceValidity(Graph _graph, bool isValidSource)
        {
            var nodes = _graph.GetNodes();
        
            List<Node> _validSourceNodes = new List<Node>();

            logger.Debug("{0} Nodes in the graph", nodes.Length);

            foreach(Node node in nodes)
            {
                if(node.ValidSource==isValidSource)
                    _validSourceNodes.Add(node);
            }

            var validSourceNodes = nodes.Where(n => n.ValidSource==isValidSource);

            logger.Debug("{0} ({1}) Nodes where ValidSource is {2} ",_validSourceNodes.Count,validSourceNodes.Count(),isValidSource);
        }

        public static void SearchForEdgesBySourceValidityAndTransportMode(Graph _graph, bool isValidSource, byte transportModes)
        {
            var nodes = _graph.GetNodes();
            var validSourceNodes = nodes.Where(n=>n.ValidSource==isValidSource);
            var outwardEdges = validSourceNodes.SelectMany(t=>t.OutwardEdges).ToArray();
            var outwardEdgesWithSelectedTransportMode = outwardEdges.Where(oe=>(oe.TransportModes & transportModes) == transportModes);

            logger.Debug("{0,10} Nodes in the graph", nodes.Length);
            logger.Debug("{0,10} Nodes where ValidSource is {1} ",validSourceNodes.Count(),isValidSource);
            logger.Debug("{0,10} Outward Edges in the selected Nodes",outwardEdges.Length);
            logger.Debug("{0,10} Outward Edges in the selected Nodes with transport mode(s) '{1}'",outwardEdgesWithSelectedTransportMode.Count(),TransportModes.MaskToString(transportModes));
            
            // foreach(var node in validSourceNodes)
            // {
            //     if((node.GetAvailableOutboundTransportModes() & transportModes)==transportModes)
            //     {
            //         _graph.TraceOneNode(node);
            //     }
            // }
        }

        public static void TraceEdgesBySourceTargetValidities(Graph _graph, bool isValidSource, bool isValidTarget)
        {
            var nodes = _graph.GetNodes();

            var transportModes = TransportModes.GetTransportModes();
            var routeTypes =  TransportModes.GetRouteTypes();
            
            var validSourceNodes = nodes.Where(n=>n.ValidSource==isValidSource);
            var outwardEdges = validSourceNodes.SelectMany(t=>t.OutwardEdges).ToArray();

            var validTargetNodes = nodes.Where(n=>n.ValidTarget==isValidTarget);
            var inwardEdges = validTargetNodes.SelectMany(t=>t.InwardEdges).ToArray();

            logger.Debug("{0,10} Nodes in the graph", nodes.Length);

            logger.Debug("{0,10} Nodes where ValidSource is {1} ",validSourceNodes.Count(),isValidSource);
            logger.Debug("{0,10} Outward Edges in the selected Nodes",outwardEdges.Length);
            foreach(var transportMode in transportModes)
            {
                var outwardEdgesWithSelectedTransportMode = outwardEdges.Where(oe=>(oe.TransportModes & transportMode) == transportMode);
                logger.Debug("{0,10} Outward Edges in the selected Nodes with transport mode '{1}'",outwardEdgesWithSelectedTransportMode.Count(),TransportModes.SingleMaskToString(transportMode));
            }
            foreach(var routeType in routeTypes)
            {
                var outwardEdgesWithSelectedTransportMode = outwardEdges.Where(oe=>oe.TagIdRouteType==routeType);
                logger.Debug("{0,10} Outward Edges in the selected Nodes with route type {1} ('{2}')",outwardEdgesWithSelectedTransportMode.Count(),routeType,TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType)?TransportModes.OSMTagIdToKeyValue[routeType]:"");
            }

            logger.Debug("{0,10} Nodes where ValidTarget is {1} ",validTargetNodes.Count(),isValidTarget);
            logger.Debug("{0,10} Inward Edges in the selected Nodes",inwardEdges.Length);
            foreach(var transportMode in transportModes)
            {
                var inwardEdgesWithSelectedTransportMode = inwardEdges.Where(ie=>(ie.TransportModes & transportMode) == transportMode);
                logger.Debug("{0,10} Inward Edges in the selected Nodes with transport mode '{1}'",inwardEdgesWithSelectedTransportMode.Count(),TransportModes.SingleMaskToString(transportMode));
            }
            foreach(var routeType in routeTypes)
            {
                var inwardEdgesWithSelectedTransportMode = inwardEdges.Where(ie=>ie.TagIdRouteType==routeType);
                logger.Debug("{0,10} Inward Edges in the selected Nodes with route type {1} ('{2}')",inwardEdgesWithSelectedTransportMode.Count(),routeType,TransportModes.OSMTagIdToKeyValue.ContainsKey(routeType)?TransportModes.OSMTagIdToKeyValue[routeType]:"");
            }


            // foreach(var node in validSourceNodes)
            // {
            //     if((node.GetAvailableOutboundTransportModes() & transportModes)==transportModes)
            //     {
            //         _graph.TraceOneNode(node);
            //     }
            // }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Destination not reached issue

        public static void EviscerateStep(DijkstraStep step, Dictionary<int, double> bestScoreForNode)
        {
            if(step.PreviousStep is not null)
            {
                Console.WriteLine(@"Previous step :: ActiveNodeId: {0}, 
                                                    CumulatedCost: {1}, 
                                                    Direction: {2}, 
                                                    SequenceIndex: {3}, 
                                                    TransportMode: {4}, 
                                                    OutboundRouteType: {5}",
                                                    step.PreviousStep.ActiveNode.Idx,
                                                    step.PreviousStep.CumulatedCost,
                                                    step.PreviousStep.Direction,
                                                    step.PreviousStep.TransportSequenceIndex,
                                                    TransportModes.SingleMaskToString(step.PreviousStep.InboundTransportMode),
                                                    step.PreviousStep.InboundRouteType);

                TraceOneNode(step.PreviousStep.ActiveNode);
                
            }
            else
            {
                Console.WriteLine(@"Previous step :: null"); 
            }

            Console.WriteLine(@"Current step :: ActiveNodeId: {0}, 
                                                    CumulatedCost: {1}, 
                                                    Direction: {2}, 
                                                    SequenceIndex: {3}, 
                                                    TransportMode: {4}, 
                                                    OutboundRouteType: {5}",
                                                    step.ActiveNode.Idx,
                                                    step.CumulatedCost,
                                                    step.Direction,
                                                    step.TransportSequenceIndex,
                                                    TransportModes.SingleMaskToString(step.InboundTransportMode),
                                                    step.InboundRouteType);

            TraceOneNode(step.ActiveNode);

            Console.WriteLine("Node Scores:");
            Console.WriteLine("Node: Cumulated cost");
            // foreach(var score in bestScoreForNode)
            // {
            //     Console.WriteLine("{0,10}:{1,30}", score.Key,score.Value);
            // }

        }

        public static void TraceOneNode(Node node)
        {
            logger.Info("Idx = {0}, OsmId =  {1}, nb in {2}, nb out {3}, idx {4}, coord = {5} {6}, T = {7}, s = {8}", node.Idx,
            node.OsmID, node.InwardEdges.Count, node.OutwardEdges.Count, node.Idx, node.Y, node.X, node.ValidTarget, node.ValidSource);
            TraceEdges(node);

            var availableInboundTransportModes = TransportModes.MaskToString(node.GetAvailableInboundTransportModes());
            var availableOutboundTransportModes = TransportModes.MaskToString(node.GetAvailableOutboundTransportModes());
            logger.Debug("Available Inbound Transport Modes for Node {0}: {1}", node.OsmID, availableInboundTransportModes);
            logger.Debug("Available Outbound Transport Modes for Node {0}: {1}", node.OsmID, availableOutboundTransportModes);
            logger.Debug("\n");
        }
        private static void TraceEdges(Node node)
        {
            logger.Info("\tInward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.InwardEdges)
            {
                TraceEdge(edge);
            }

            logger.Info("\tOutward Edges in Node {0}:", node.OsmID);
            foreach (var edge in node.OutwardEdges)
            {
                TraceEdge(edge);
            }
        }

        private static void TraceEdge(Edge edge)
        {
             logger.Info("\t\t > Edge: \tSource Id: {0} ({1},{2});\tTarget Id: {3} ({4},{5});\tTransport Modes: {6}, length = {7} (m), speed = {8} (m/2), Route type = {9}",
                    edge.SourceNode?.Idx, edge.SourceNode?.X, edge.SourceNode?.Y, edge.TargetNode?.Idx, edge.TargetNode?.X, edge.TargetNode?.Y, TransportModes.MaskToString(edge.TransportModes), edge.LengthM,edge.MaxSpeedMPerS, edge.TagIdRouteType);
            // logger.Info("\t\t > Edge: {0},\tcost: {1},\tSource Id: {2} ({3},{4});\tTarget Id: {5} ({6},{7});\tTransport Modes: {8} (mask: {9}) length = {10} speed = {11}",
            //         edge.OsmID, edge.Cost, edge.SourceNode?.OsmID, edge.SourceNode?.X, edge.SourceNode?.Y, edge.TargetNode?.OsmID, edge.TargetNode?.X, edge.TargetNode?.Y, TransportModes.MaskToString(edge.TransportModes), edge.TransportModes,edge.LengthM,edge.MaxSpeedMPerS);

            TraceInternalGeometry(edge);
        }

        private static void TraceInternalGeometry(Edge edge)
        {
            if (edge.InternalGeometry is not null)
            {
                logger.Debug("\t\t   Internal geometry in Edge {0}:", edge.OsmID);
                foreach (var xymPoint in edge.InternalGeometry)
                {
                    logger.Debug("\t\t\tX: {0},\tY: {1},\tM: {2};",
                        xymPoint.X, xymPoint.Y, xymPoint.M);
                }
            }
            else
            {
                logger.Debug("\t\t   No Internal geometry in Edge {0}:", edge.OsmID);
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Pedestrian-Metro timing issue

        public static void ExposeTransportTransitions(List<Node> nodeRoute, Persona persona)
        {
            LineString pointRoute = persona.Route!;
            Dictionary<int,Tuple<byte,int>> bTransitions = persona.TransportModeTransitions!;
            Tuple<string[],DateTime[]> tTransitions = persona.TTextTransitions;

            Node[] nodes = nodeRoute.ToArray();

            int tTransition=0;
            string[] modes = tTransitions.Item1;
            DateTime[] stamps = tTransitions.Item2;

            Console.WriteLine("Nodes:");
            Console.WriteLine("\tIdx:\tTransport Mode:\tRoute Type:");
            for(int n=0; n<nodes.Length; n++)
            {
                string transportMode = "";
                int routeType = 0;
                if(bTransitions.ContainsKey(nodes[n].Idx))
                {
                    transportMode = TransportModes.SingleMaskToString(bTransitions[nodes[n].Idx].Item1);
                    routeType = bTransitions[nodes[n].Idx].Item2;
                    Console.WriteLine("{0}\t{1}\t{2}\t\t{3}",n,nodes[n].Idx,transportMode,routeType);
                }
            }
            Console.WriteLine("Coordinates:");
            Console.WriteLine("\tX:\tY:\tM:\t\tDate-Time:\t\tTime Stamp:\t\tTransport Mode:");
            Coordinate[] points = pointRoute.Coordinates;
            for(int i=0; i<points.Length; i++)
            {
                string timeStampS = "";
                string transportModeS = "";
                DateTime timeStamp = Constants.BaseDateTime.Add(TimeSpan.FromSeconds(points[i].M));
                if(stamps[tTransition]==timeStamp)
                {
                    timeStampS = stamps[tTransition].ToString();
                    transportModeS = modes[tTransition].ToString();
                    tTransition++;
                }
                Console.WriteLine("{0:###0}\t{1:0.0000}\t{2:00.0000}\t{3:0000.0000}\t{4}\t{5}\t{6}",i,points[i].X,points[i].Y,points[i].M,timeStamp,timeStampS,transportModeS);

            } 
        }

        public static void ExposeTransportTransitionsNodeSeries(List<Node> nodeRoute, Persona persona)
        {
            LineString pointRoute = persona.Route!;
            Dictionary<int,Tuple<byte,int>> bTransitions = persona.TransportModeTransitions!;
            Tuple<string[],DateTime[]> tTransitions = persona.TTextTransitions;

            Node[] nodes = nodeRoute.ToArray();

            string[] modes = tTransitions.Item1;
            DateTime[] stamps = tTransitions.Item2;

            Console.WriteLine("Nodes:");
            Console.WriteLine("\tIdx:\tTransport Mode:\tRoute Type:");
            for(int n=0; n<nodes.Length; n++)
            {
                string transportMode = "";
                int routeType = 0;
                if(bTransitions.ContainsKey(nodes[n].Idx))
                {
                    transportMode = TransportModes.SingleMaskToString(bTransitions[nodes[n].Idx].Item1);
                    routeType = bTransitions[nodes[n].Idx].Item2;
                    Console.WriteLine("{0}\t{1}\t{2}\t\t{3}",n,nodes[n].Idx,transportMode,routeType);
                }
            } 
        }

        public static void ExposeTransportTransitionsTimeSeries(List<Node> nodeRoute, Persona persona)
        {
            LineString pointRoute = persona.Route!;
            Dictionary<int,Tuple<byte,int>> bTransitions = persona.TransportModeTransitions!;
            Tuple<string[],DateTime[]> tTransitions = persona.TTextTransitions;

            Node[] nodes = nodeRoute.ToArray();

            int tTransition=0;
            string[] modes = tTransitions.Item1;
            DateTime[] stamps = tTransitions.Item2;
            
            Console.WriteLine("Coordinates:");
            Console.WriteLine("\tX:\tY:\tM:\t\tDate-Time:\t\tTime Stamp:\t\tTransport Mode:");
            Coordinate[] points = pointRoute.Coordinates;
            for(int i=0; i<points.Length; i++)
            {
                string timeStampS = "";
                string transportModeS = "";
                DateTime timeStamp = Constants.BaseDateTime.Add(TimeSpan.FromSeconds(points[i].M));
                if(stamps[tTransition]==timeStamp)
                {
                    timeStampS = stamps[tTransition].ToString();
                    transportModeS = modes[tTransition].ToString();
                    tTransition++;
                }
                Console.WriteLine("{0:###0}\t{1:0.0000}\t{2:00.0000}\t{3:0000.0000}\t{4}\t{5}\t{6}",i,points[i].X,points[i].Y,points[i].M,timeStamp,timeStampS,transportModeS);

            }
            
        }

        public static void ExposeNodeToLineStringStep(int s, double x, double y, double m, int idx, int ni, int nj, byte aitm, byte aotm, byte itm, byte notm, byte otm)
        {
            DateTime ts = Constants.BaseDateTime.Add(TimeSpan.FromSeconds(m));
            string idxS = idx>=0?idx.ToString():"--------";
            string niS = ni>=0?ni.ToString():"---";
            string njS = nj>=0?nj.ToString():"---";
            Console.WriteLine("{0:###0}\t{1:0.0000000}\t{2:00.0000000}\t{3:0000.000}\t{4}\t{5,8} ({6,6})[{7,15}]::{8,50} {9,50}\t{10}\t\t{11}\t\t\t{12}",
                                s,        x,             y,              m,            ts,  idxS,  niS,   njS,
                                                                                    TransportModes.MaskToString(aitm),
                                                                                            TransportModes.MaskToString(aotm),
                                                                                                TransportModes.SingleMaskToString(itm),
                                                                                                        TransportModes.SingleMaskToString(notm),
                                                                                                            TransportModes.SingleMaskToString(otm));
            //Environment.Exit(0);                                                                                                            
        }

        public static void DisplayRouteReconstruction(DijkstraStep? lastStep)
        {
            Console.WriteLine("Route reconstruction step by step (backwards):");
            List<DijkstraStep> steps = new List<DijkstraStep>(0);
            DijkstraStep step = lastStep!;
            List<Node> nodes = new List<Node>(0);
            Node node = step.ActiveNode;
            nodes.Add(node);

            steps.Add(step);

            while(step.PreviousStep!=null)
            {
                step=step.PreviousStep;
                node=step.ActiveNode;
                steps.Add(step);
                nodes.Add(node);
            }
            Console.WriteLine("Route reconstruction step by step (backwards): done.");

            int count=0;
            Console.WriteLine("{0,3}\t{1,14}\t{2,16}\t{3,20}\t{4,25}\t{5,20}\t{6,15}",
                        "count",
                        "ActiveNode.Idx",
                        "PreviousNode.Idx",
                        "CumulatedCost",
                        "TransportSequenceIndex",
                        "InboundTransportMode",
                        "InboundRouteType"
                        );
            foreach(var s in steps)
            {
                Console.WriteLine("{0,3}\t{1,14}\t{2,16}\t{3,20}\t{4,25}\t{5,20}\t{6,15}",
                    count++,
                    s.ActiveNode.Idx,
                    s.PreviousStep!=null?s.PreviousStep.ActiveNode.Idx:"--------",
                    s.CumulatedCost,
                    s.TransportSequenceIndex,
                    s.InboundTransportMode,
                    s.InboundRouteType);
            }
            Console.WriteLine("----------------------------------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("{0,3}\t{1,8}\t{2,50}\t{3,50}",
                        "count",
                        "Idx",
                        "Available inbound modes",
                        "Available outbound modes"
                        );
            foreach(var n in nodes)
            {
                Console.WriteLine("{0,3}\t{1,8}\t{2,50}\t{3,50}",
                    --count,
                    n.Idx,
                    TransportModes.MaskToString(n.GetAvailableInboundTransportModes()),
                    TransportModes.MaskToString(n.GetAvailableOutboundTransportModes()));
            }
            Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------");

            //Environment.Exit(0);
        }





        
    }
}