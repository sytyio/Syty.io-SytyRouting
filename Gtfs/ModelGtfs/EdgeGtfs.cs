using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.Gtfs.ModelGtfs
{

   // If IsShapeAvailable is false => SourceNearestLineString, TargetNearestLineString, WalkDistanceSourceM, WalkDistanceTargetM, DistanceNearestPointsM are irrelevant
    public class EdgeGtfs: Edge
    {
        public string Id { get; }


        //  If there is a linestring SourceNode and TargetNode will contain a node, otherwise, will contain the StopGtfs
        public double DurationS { get; }

        public RouteGtfs Route { get; }

        public double DistanceSourceToTargetM { get; }

        public bool IsShapeAvailable { get; }

        // If there is a linestring will contain the initial Stop, otherwise, will be null
        public StopGtfs? InitialStopSource { get; }
        public StopGtfs? InitialStopTarget { get; }

        

        public override string ToString()
        {
            // + "MaxSpeepMPerS = "+MaxSpeedMPerS+" Target on linestring "+TargetNearestLineString +  " Distance = " + DistanceSourceToTargetM + " meters, Duration = " + DurationS + " seconds" + "Source on linestring = "+ SourceNearestLineString + " walkSource = " +WalkDistanceSourceM + " walktarget = "+ WalkDistanceTargetM +" DistanceBetween = "+DistanceNearestPointsM
            return "Id = " + Id + " Target = " + TargetNode + " Source = " + SourceNode + " Route = " + Route.LongName +Route.Id + " LineString? = " + IsShapeAvailable + "MaskMode = "+TransportModes;
        }

        public EdgeGtfs(string id, Node source, Node target, double distance, double duration, RouteGtfs route, bool iShapeAvailable, StopGtfs? initialStopSource, StopGtfs? initialStopTarget, double maxSpeedMPerS, XYMPoint[]? internalGeometry,byte transportModes)
        {
            OsmID = long.MaxValue;
            DistanceSourceToTargetM = distance;
            DurationS = duration;
            Id = id;
            TargetNode = target;
            SourceNode = source;
            Route = route;
            IsShapeAvailable = iShapeAvailable;
            InitialStopSource = initialStopSource;
            InitialStopTarget = initialStopTarget;
            MaxSpeedMPerS = maxSpeedMPerS;
            InternalGeometry = internalGeometry;
            TransportModes = transportModes;
        }
    }
}