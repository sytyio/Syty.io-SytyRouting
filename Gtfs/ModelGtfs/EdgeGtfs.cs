using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.Gtfs.ModelGtfs
{

   // If IsShapeAvailable is false => SourceNearestLineString, TargetNearestLineString, WalkDistanceSourceM, WalkDistanceTargetM, DistanceNearestPointsM are irrelevant
    public class EdgeGtfs: Edge
    {
        public string Id { get; }

        public StopGtfs SourceStop { get; }

        public StopGtfs TargetStop { get; }

        public double DurationS { get; }

        public RouteGtfs Route { get; }

        public double DistanceSourceToTargetM { get; }

        public bool IsShapeAvailable { get; }

        public Point? SourceNearestLineString { get; }

        public Point? TargetNearestLineString { get; }

        public double? WalkDistanceSourceM { get; }

        public double? WalkDistanceTargetM { get; }

        public double? DistanceNearestPointsM { get; }

        

        public override string ToString()
        {
            // + "MaxSpeepMPerS = "+MaxSpeedMPerS+" Target on linestring "+TargetNearestLineString +  " Distance = " + DistanceSourceToTargetM + " meters, Duration = " + DurationS + " seconds" + "Source on linestring = "+ SourceNearestLineString + " walkSource = " +WalkDistanceSourceM + " walktarget = "+ WalkDistanceTargetM +" DistanceBetween = "+DistanceNearestPointsM
            return "Id = " + Id + " Target = " + TargetStop + " Source = " + SourceStop + " Route = " + Route + " LineString? = " + IsShapeAvailable;
        }

        // public override bool Equals(object? obj)
        // {
        //     return obj is EdgeGtfs gtfs &&
        //            OsmID == gtfs.OsmID &&
        //            Cost == gtfs.Cost &&
        //            LengthM == gtfs.LengthM &&
        //            MaxSpeedMPerS == gtfs.MaxSpeedMPerS &&
        //            EqualityComparer<Node?>.Default.Equals(SourceNode, gtfs.SourceNode) &&
        //            EqualityComparer<Node?>.Default.Equals(TargetNode, gtfs.TargetNode) &&
        //            EqualityComparer<XYMPoint[]?>.Default.Equals(InternalGeometry, gtfs.InternalGeometry) &&
        //            Id == gtfs.Id &&
        //            EqualityComparer<StopGtfs>.Default.Equals(SourceStop, gtfs.SourceStop) &&
        //            EqualityComparer<StopGtfs>.Default.Equals(TargetStop, gtfs.TargetStop) &&
        //            DurationS == gtfs.DurationS &&
        //            EqualityComparer<RouteGtfs>.Default.Equals(Route, gtfs.Route) &&
        //            DistanceSourceToTargetM == gtfs.DistanceSourceToTargetM &&
        //            IsShapeAvailable == gtfs.IsShapeAvailable &&
        //            EqualityComparer<Point?>.Default.Equals(SourceNearestLineString, gtfs.SourceNearestLineString) &&
        //            EqualityComparer<Point?>.Default.Equals(TargetNearestLineString, gtfs.TargetNearestLineString) &&
        //            WalkDistanceSourceM == gtfs.WalkDistanceSourceM &&
        //            WalkDistanceTargetM == gtfs.WalkDistanceTargetM &&
        //            DistanceNearestPointsM == gtfs.DistanceNearestPointsM;
        // }

        // public override int GetHashCode()
        // {
        //     HashCode hash = new HashCode();
        //     hash.Add(OsmID);
        //     hash.Add(Cost);
        //     hash.Add(LengthM);
        //     hash.Add(MaxSpeedMPerS);
        //     hash.Add(SourceNode);
        //     hash.Add(TargetNode);
        //     hash.Add(InternalGeometry);
        //     hash.Add(Id);
        //     hash.Add(SourceStop);
        //     hash.Add(TargetStop);
        //     hash.Add(DurationS);
        //     hash.Add(Route);
        //     hash.Add(DistanceSourceToTargetM);
        //     hash.Add(IsShapeAvailable);
        //     hash.Add(SourceNearestLineString);
        //     hash.Add(TargetNearestLineString);
        //     hash.Add(WalkDistanceSourceM);
        //     hash.Add(WalkDistanceTargetM);
        //     hash.Add(DistanceNearestPointsM);
        //     return hash.ToHashCode();
        // }

        public EdgeGtfs(string id, StopGtfs source, StopGtfs target, double distance, double duration, RouteGtfs route, bool iShapeAvailable, Point? sourceNearestLineString, Point? targetNearestLineString, double walkDistanceSourceM, double walkDistanceTargetM, double distanceNearestPointsM, double maxSpeedMPerS, XYMPoint[]? internalGeometry)
        {
            OsmID = long.MaxValue;
            DistanceSourceToTargetM = distance;
            DurationS = duration;
            Id = id;
            TargetStop = target;
            TargetNode=target;
            SourceStop = source;
            SourceNode=source;
            Route = route;
            IsShapeAvailable = iShapeAvailable;
            SourceNearestLineString = sourceNearestLineString;
            TargetNearestLineString = targetNearestLineString;
            WalkDistanceSourceM = walkDistanceSourceM;
            WalkDistanceTargetM = walkDistanceTargetM;
            DistanceNearestPointsM = distanceNearestPointsM;
            MaxSpeedMPerS = maxSpeedMPerS;
            InternalGeometry = internalGeometry;

        }
    }
}