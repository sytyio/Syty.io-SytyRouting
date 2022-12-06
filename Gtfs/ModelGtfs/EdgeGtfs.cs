using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class EdgeGtfs: Edge
    {
        public string Id { get; }

        //  If there is a linestring SourceNode and TargetNode will contain a node, otherwise, will contain the StopGtfs
        public double DurationS { get; }

        public RouteGtfs Route { get; }
        public bool IsShapeAvailable { get; }
 
        

        public override string ToString()
        {
            // + "MaxSpeepMPerS = "+MaxSpeedMPerS+" Target on linestring "+TargetNearestLineString +  " Distance = " + DistanceSourceToTargetM + " meters, Duration = " + DurationS + " seconds" + "Source on linestring = "+ SourceNearestLineString + " walkSource = " +WalkDistanceSourceM + " walktarget = "+ WalkDistanceTargetM +" DistanceBetween = "+DistanceNearestPointsM
            return "Id = " + Id + " Target = " + TargetNode.Y+" "+TargetNode.X + " Source = " + SourceNode.Y+ " "+SourceNode.X + " Length ="+LengthM+ " Route = " + Route.LongName +Route.Id + " LineString? = " + IsShapeAvailable + "MaskMode = "+TransportModes;
        }

        public EdgeGtfs(string id, Node source, Node target, double distance, double duration, RouteGtfs route, bool iShapeAvailable,  double maxSpeedMPerS, XYMPoint[]? internalGeometry,byte transportModes)
        {
            OsmID = long.MaxValue; 
            LengthM = distance;
            DurationS = duration;
            Id = id;
            TargetNode = target;
            SourceNode = source;
            Route = route;
            IsShapeAvailable = iShapeAvailable;
            MaxSpeedMPerS = maxSpeedMPerS;
            InternalGeometry = internalGeometry;
            TransportModes = transportModes;
        }
    }
}