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
        protected Dictionary<int, Tuple<byte,int>> transportModeTransitions = new Dictionary<int, Tuple<byte,int>>(1);
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

        public LineString NodeRouteToLineStringMMilliseconds(List<Node> nodeRoute, TimeSpan initialTimeStamp)
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);

            if(nodeRoute.Count <= 1)
            {
                return new LineString(null, geometryFactory);
            }
            
            List<Coordinate> xyCoordinates = new List<Coordinate>(0);
            List<double> mOrdinates = new List<double>(0);

            //DEBUG:
            double currentM=0.0;
            double previousM=-1.0;
            bool mSequenceInconsistency=false;
            //

            var sourcePointX = nodeRoute[0].X;
            var sourcePointY = nodeRoute[0].Y;
            var previousTimeIntervalMilliseconds = initialTimeStamp.TotalMilliseconds;

            xyCoordinates.Add(new Coordinate(sourcePointX, sourcePointY));
            mOrdinates.Add(previousTimeIntervalMilliseconds);

            //DEBUG:
            currentM=previousTimeIntervalMilliseconds;
            //


            for(var i = 0; i < nodeRoute.Count-1; i++)
            {          
                var edge = nodeRoute[i].OutwardEdges.Find(e => e.TargetNode.Idx == nodeRoute[i+1].Idx);
                if(edge is not null)
                {
                    if(edge.MaxSpeedMPerS==0)
                    {
                        Console.WriteLine("Edge speed is zero. (Node Idx: {0})", nodeRoute[i].Idx);
                        return new LineString(null, geometryFactory);
                    }
                    
                    var minTimeIntervalMilliseconds = edge.LengthM / edge.MaxSpeedMPerS * 1000; // [s]*[1000 ms / 1s]

                    if(edge.InternalGeometry is not null)
                    {
                        for(var j = 0; j < edge.InternalGeometry.Length; j++)
                        {
                            var internalPointX = edge.InternalGeometry[j].X;
                            var internalPointY = edge.InternalGeometry[j].Y;
                            var internalPointM = edge.InternalGeometry[j].M * minTimeIntervalMilliseconds + previousTimeIntervalMilliseconds;

                            xyCoordinates.Add(new Coordinate(internalPointX, internalPointY));
                            mOrdinates.Add(internalPointM);
                            //DEBUG:
                            previousM=currentM;
                            currentM=internalPointM;
                            if(previousM==currentM)
                            {
                                mSequenceInconsistency=true;
                                Console.WriteLine("previous M = current M");
                            }
                            //
                        }
                    }

                    var targetPointX = edge.TargetNode.X;
                    var targetPointY = edge.TargetNode.Y;

                    previousTimeIntervalMilliseconds = minTimeIntervalMilliseconds + previousTimeIntervalMilliseconds;

                    xyCoordinates.Add(new Coordinate(targetPointX, targetPointY));
                    mOrdinates.Add(previousTimeIntervalMilliseconds);

                    //DEBUG:
                    previousM=currentM;
                    currentM=previousTimeIntervalMilliseconds;
                    if(previousM==currentM)
                    {
                        mSequenceInconsistency=true;
                        Console.WriteLine("previous M = current M");
                    }
                    //
                }
                else
                {
                    return new LineString(null, geometryFactory);
                }
                //DEBUG:
                // if(!isValidSequence(mOrdinates.ToArray()))
                // {
                //     double prevM=0.0;
                //     foreach(var m in mOrdinates)
                //     {   
                //         if(m<=prevM)
                //         {
                //             Console.WriteLine("----------------", m);
                //         }
                //         Console.WriteLine("M: {0}", m);
                //         if(m<=prevM)
                //         {
                //             Console.WriteLine("----------------", m);
                //         }
                //         prevM=m;
                //     }                    
                //     Console.WriteLine("At least one previous M = current M");
                // }
                //
            }

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();


            //DEBUG:
            if(mSequenceInconsistency)
                return new LineString(null, geometryFactory);
            else //
                return new LineString(coordinateSequence, geometryFactory);
            
        }

        private bool isValidSequence(double[] m)
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

        public double GetRouteCost()
        {
            return routeCost;
        }

        public Dictionary<int,Tuple<byte,int>> GetTransportModeTransitions()
        {
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