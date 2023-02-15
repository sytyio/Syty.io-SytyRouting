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

        // public LineString NodeRouteToLineStringMSeconds(double startX, double startY, double endX, double endY, List<Node> nodeRoute, TimeSpan initialTimeStamp)
        // {
        //     var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
        //     var geometryFactory = new GeometryFactory(sequenceFactory);

        //     var points = nodeRoute.Count;

        //     if(points <= 1)
        //     {
        //         return new LineString(null, geometryFactory);
        //     }
            
        //     List<Coordinate> xyCoordinates = new List<Coordinate>(points+2); // points +1 start point (home) +1 end point (work)
        //     List<double> mOrdinates = new List<double>(points+2);

        //     var firstNodeX = nodeRoute.First().X;
        //     var firstNodeY = nodeRoute.First().Y;

        //     var previousTimeInterval = initialTimeStamp.TotalSeconds;

        //     byte previousTransportMode = TransportModes.None;
        //     byte currentTransportMode = TransportModes.DefaultMode;
            
        //     xyCoordinates.Add(new Coordinate(startX,startY));
        //     mOrdinates.Add(previousTimeInterval);

        //     //debug:
        //     int step=0;
        //     int ni=-1;
        //     int nj=-1;
        //     double x=startX;
        //     double y=startY;
        //     double m=previousTimeInterval;
        //     int idx=-1;
        //     byte aitm=0;
        //     byte aotm=0;
        //     byte ptm=TransportModes.None;
        //     byte stm=TransportModes.None;
        //     byte ctm=TransportModes.DefaultMode;
        //     TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
        //     //

        //     previousTimeInterval = Get2PointTimeInterval(startX,startY,firstNodeX,firstNodeY,currentTransportMode);

        //     xyCoordinates.Add(new Coordinate(firstNodeX,firstNodeY));
        //     mOrdinates.Add(previousTimeInterval);

        //     //debug:
        //     x=firstNodeX;
        //     y=firstNodeY;
        //     m=previousTimeInterval;
        //     idx=nodeRoute.First().Idx;
        //     aitm=nodeRoute.First().GetAvailableInboundTransportModes();
        //     aotm=nodeRoute.First().GetAvailableOutboundTransportModes();
        //     ni=0;
        //     TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
        //     //

        //     for(var i = 0; i < nodeRoute.Count-1; i++)
        //     {
        //         var selectedTransportMode = SelectTransportMode(nodeRoute[i].Idx, transportModeTransitions);
        //         //debug:
        //         stm=selectedTransportMode;
        //         //

        //         var edge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);
        //         //debug:
        //         var edge2 = FindEdge(nodeRoute[i], nodeRoute[i+1], selectedTransportMode);
        //         try
        //         {
        //             Console.WriteLine("edges: {0}:{1}::{2}",edge!.OsmID,edge2.OsmID,edge!.OsmID==edge2.OsmID?"COMPUTER OK":"!!!COMPUTER NOT OK!!!");
        //         }
        //         catch
        //         {
        //             Console.WriteLine("edge: {0}, type: {1}",edge!.OsmID,edge.TagIdRouteType);
        //         }
                
        //         //

        //         //debug:
        //         ni=i+1;
        //         idx=nodeRoute[i+1].Idx;
        //         aitm=nodeRoute[i+1].GetAvailableInboundTransportModes();
        //         aotm=nodeRoute[i+1].GetAvailableOutboundTransportModes();
        //         //

        //         if(edge is not null)
        //         {
        //             // if(edge.MaxSpeedMPerS==0)
        //             // {
        //             //     logger.Debug("Edge speed is zero. (Node Idx: {0})", nodeRoute[i].Idx);
        //             //     return new LineString(null, geometryFactory);
        //             // }

        //             if(selectedTransportMode!=currentTransportMode && selectedTransportMode!=TransportModes.None && selectedTransportMode!=currentTransportMode)
        //             {
        //                 previousTransportMode = currentTransportMode;
        //                 currentTransportMode = selectedTransportMode;

        //                 //debug:
        //                 ptm=previousTransportMode;
        //                 ctm=currentTransportMode;
        //                 //
        //             }

        //             var speed = Helper.ComputeSpeed(edge, currentTransportMode);
                    
        //             //var minTimeIntervalS = edge.LengthM / edge.MaxSpeedMPerS; // [s]
        //             var minTimeIntervalS = edge.LengthM / speed; // [s]

        //             minTimeIntervalS = Helper.ApplyRoutingPenalties(edge, currentTransportMode, minTimeIntervalS);

        //             if(edge.InternalGeometry is not null)
        //             {
        //                 for(var j = 0; j < edge.InternalGeometry.Length; j++)
        //                 {
        //                     //debug:
        //                     nj=j;
        //                     //
        //                     var internalX = edge.InternalGeometry[j].X;
        //                     var internalY = edge.InternalGeometry[j].Y;
        //                     var internalM = edge.InternalGeometry[j].M * minTimeIntervalS + previousTimeInterval;

        //                     xyCoordinates.Add(new Coordinate(internalX, internalY));
        //                     mOrdinates.Add(internalM);

        //                     //debug:
        //                     x=internalX;
        //                     y=internalY;
        //                     m=internalM;
        //                     TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
        //                     //
        //                 }
        //                 //debug:
        //                 nj=-1;
        //                 //
        //             }

        //             var targetX = edge.TargetNode.X;
        //             var targetY = edge.TargetNode.Y;

        //             previousTimeInterval = minTimeIntervalS + previousTimeInterval;

        //             xyCoordinates.Add(new Coordinate(targetX, targetY));
        //             mOrdinates.Add(previousTimeInterval);

        //             //debug:
        //             x=targetX;
        //             y=targetY;
        //             m=previousTimeInterval;
        //             idx=edge.TargetNode.Idx;
        //             // ptm=previousTransportMode;
        //             // stm=TransportModes.None;
        //             // ctm=currentTransportMode;
        //             TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
        //             //
        //         }
        //         else
        //         {
        //             return new LineString(null, geometryFactory);
        //         }
        //         //debug:
        //         //stm=TransportModes.None;
        //         //
        //     }

        //     var lastNodeX = nodeRoute.Last().X;
        //     var lastNodeY = nodeRoute.Last().Y;

        //     //debug:
        //     ni=nodeRoute.Count-1;
        //     aitm=nodeRoute.Last().GetAvailableInboundTransportModes();
        //     aotm=nodeRoute.Last().GetAvailableOutboundTransportModes();
        //     //

        //     var endM = Get2PointTimeInterval(lastNodeX,lastNodeY,endX,endY,TransportModes.DefaultMode) + previousTimeInterval;
            
        //     xyCoordinates.Add(new Coordinate(endX,endY));
        //     mOrdinates.Add(endM);

        //     //debug:
        //     x=lastNodeX;
        //     y=lastNodeY;
        //     m=endM;
        //     idx=nodeRoute.Last().Idx;
        //     ctm=TransportModes.DefaultMode;
        //     TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
        //     //

        //     var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
        //     for(var i = 0; i < coordinateSequence.Count; i++)
        //     {
        //         coordinateSequence.SetM(i, mOrdinates[i]);
        //     }
        //     coordinateSequence.ReleaseCoordinateArray();

        //     return new LineString(coordinateSequence, geometryFactory);
        // }

        public LineString NodeRouteToLineStringMSeconds(double startX, double startY, double endX, double endY, List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
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

            byte previousTransportMode = TransportModes.None;
            byte currentTransportMode = TransportModes.DefaultMode;
            
            xyCoordinates.Add(new Coordinate(startX,startY));
            mOrdinates.Add(previousTimeInterval);

            //debug:
            Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("{0:###0}\t{1:0.0000000}\t\t{2:00.0000000}\t\t{3:0000.000}\t\t{4}\t\t{5,8} ({6,3})[{7,3}]::{8,50} {9,50}:\t{10}\t{11}\t{12}",
                                "STEP",   "X",             "Y",              "M",            "TIME STAMP",
                                                                                                    "NODE IDX",
                                                                                                           "node count (i)",
                                                                                                                  "internal geometry count (j)",
                                                                                                                          "AVAILABLE INBOUND MODES",
                                                                                                                                 "AVAILABLE OUTBOUND MODES",
                                                                                                                                           "PREVIOUS MODE",
                                                                                                                                                 "SELECTED MODE",
                                                                                                                                                       "CURRENT MODE");
            int step=0;
            int ni=-1;
            int nj=-1;
            double x=startX;
            double y=startY;
            double m=previousTimeInterval;
            int idx=-1;
            byte aitm=0;
            byte aotm=0;
            byte ptm=previousTransportMode;
            byte stm=TransportModes.None;
            byte ctm=currentTransportMode;
            TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
            //

            var firstNodeX = nodeRoute.First().X;
            var firstNodeY = nodeRoute.First().Y;

            previousTimeInterval = Get2PointTimeInterval(startX,startY,firstNodeX,firstNodeY,currentTransportMode);

            xyCoordinates.Add(new Coordinate(firstNodeX,firstNodeY));
            mOrdinates.Add(previousTimeInterval);

            //debug:
            x=firstNodeX;
            y=firstNodeY;
            m=previousTimeInterval;
            idx=nodeRoute.First().Idx;
            aitm=nodeRoute.First().GetAvailableInboundTransportModes();
            aotm=nodeRoute.First().GetAvailableOutboundTransportModes();
            ni=0;
            TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
            //

            for(var i = 0; i < nodeRoute.Count-1; i++)
            {
                var selectedTransportMode = SelectTransportMode(nodeRoute[i].Idx, transportModeTransitions);
                //debug:
                stm=selectedTransportMode;
                //

                if(selectedTransportMode!=TransportModes.None && selectedTransportMode!=currentTransportMode)
                {
                    previousTransportMode = currentTransportMode;
                    currentTransportMode = selectedTransportMode;

                    //debug:
                    ptm=previousTransportMode;
                    ctm=currentTransportMode;
                    //
                }

                var edge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);
                //debug:
                var edge2 = FindEdge(nodeRoute[i], nodeRoute[i+1], currentTransportMode);
                try
                {
                    Console.WriteLine("edges: {0}:{1}::{2}",edge!.OsmID,edge2.OsmID,edge!.OsmID==edge2.OsmID?"COMPUTER OK":"!!!COMPUTER NOT OK!!!");
                }
                catch
                {
                    Console.WriteLine("edge: {0}, type: {1}",edge!.OsmID,edge.TagIdRouteType);
                }
                
                //

                //debug:
                ni=i+1;
                idx=nodeRoute[i+1].Idx;
                aitm=nodeRoute[i+1].GetAvailableInboundTransportModes();
                aotm=nodeRoute[i+1].GetAvailableOutboundTransportModes();
                //

                if(edge is not null)
                {
                    // if(edge.MaxSpeedMPerS==0)
                    // {
                    //     logger.Debug("Edge speed is zero. (Node Idx: {0})", nodeRoute[i].Idx);
                    //     return new LineString(null, geometryFactory);
                    // }

                    var speed = Helper.ComputeSpeed(edge, currentTransportMode);
                    
                    //var minTimeIntervalS = edge.LengthM / edge.MaxSpeedMPerS; // [s]
                    var minTimeInterval = edge.LengthM / speed; // [s]

                    minTimeInterval = Helper.ApplyRoutingPenalties(edge, currentTransportMode, minTimeInterval);

                    if(edge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < edge.InternalGeometry.Length; j++)
                        {
                            //debug:
                            nj=j;
                            //
                            var internalX = edge.InternalGeometry[j].X;
                            var internalY = edge.InternalGeometry[j].Y;
                            var internalM = edge.InternalGeometry[j].M * minTimeInterval + previousTimeInterval;

                            xyCoordinates.Add(new Coordinate(internalX, internalY));
                            mOrdinates.Add(internalM);

                            //debug:
                            x=internalX;
                            y=internalY;
                            m=internalM;
                            TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
                            //
                        }
                        //debug:
                        nj=-1;
                        //
                    }

                    var targetX = edge.TargetNode.X;
                    var targetY = edge.TargetNode.Y;

                    previousTimeInterval = minTimeInterval + previousTimeInterval;

                    xyCoordinates.Add(new Coordinate(targetX, targetY));
                    mOrdinates.Add(previousTimeInterval);

                    //debug:
                    x=targetX;
                    y=targetY;
                    m=previousTimeInterval;
                    idx=edge.TargetNode.Idx;
                    // ptm=previousTransportMode;
                    // stm=TransportModes.None;
                    // ctm=currentTransportMode;
                    TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
                    //
                }
                else
                {
                    return new LineString(null, geometryFactory);
                }
                //debug:
                //stm=TransportModes.None;
                //
            }

            var lastNodeX = nodeRoute.Last().X;
            var lastNodeY = nodeRoute.Last().Y;

            //debug:
            ni=nodeRoute.Count-1;
            aitm=nodeRoute.Last().GetAvailableInboundTransportModes();
            aotm=nodeRoute.Last().GetAvailableOutboundTransportModes();
            //

            var endM = Get2PointTimeInterval(lastNodeX,lastNodeY,endX,endY,TransportModes.DefaultMode) + previousTimeInterval;
            
            xyCoordinates.Add(new Coordinate(endX,endY));
            mOrdinates.Add(endM);

            //debug:
            x=lastNodeX;
            y=lastNodeY;
            m=endM;
            idx=nodeRoute.Last().Idx;
            ctm=TransportModes.DefaultMode;
            TestBench.ExposeNodeToLineStringStep(step++,x,y,m,idx,ni,nj,aitm,aotm,ptm,stm,ctm);
            //

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

        public Dictionary<int,Tuple<byte,int>> GetTransportModeTransitions()
        {
            //make a copy
            Dictionary<int,Tuple<byte,int>> tmTransitions =  new Dictionary<int,Tuple<byte,int>>(transportModeTransitions.Count);
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