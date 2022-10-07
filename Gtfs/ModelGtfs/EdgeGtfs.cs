using NetTopologySuite.Geometries;

namespace SytyRouting.Gtfs.ModelGtfs
{
    // If IsShapeAvailable is false => SourceNearestLineString, TargetNearestLineString, WalkDistanceSourceM, WalkDistanceTargetM, DistanceNearestPointsM are irrelevant
    public class EdgeGtfs
    {
        public string Id { get;  }

        public StopGtfs SourceStop { get;  }

        public StopGtfs TargetStop { get;  }

        public double DurationS { get; }

        public RouteGtfs Route {get;}

        public double MaxSpeedMPerS{get;}

        public double DistanceSourceToTargetM { get;  }

        public bool IsShapeAvailable{get;}

        public Point? SourceNearestLineString {get;}

        public Point? TargetNearestLineString {get;}

        public double? WalkDistanceSourceM{get;}

        public double? WalkDistanceTargetM{get;}

        public double? DistanceNearestPointsM{get;}

        public override string ToString()
        {
            // + "MaxSpeepMPerS = "+MaxSpeedMPerS+" Target on linestring "+TargetNearestLineString +  " Distance = " + DistanceSourceToTargetM + " meters, Duration = " + DurationS + " seconds" + "Source on linestring = "+ SourceNearestLineString + " walkSource = " +WalkDistanceSourceM + " walktarget = "+ WalkDistanceTargetM +" DistanceBetween = "+DistanceNearestPointsM
            return "Id = " + Id + " Target = " + TargetStop + " Source = " + SourceStop+ " Route = " + Route +" LineString? = "+IsShapeAvailable;
        }

        public EdgeGtfs(string id, StopGtfs source, StopGtfs target, double distance, double duration, RouteGtfs route, bool iShapeAvailable, Point? sourceNearestLineString, Point? targetNearestLineString, double walkDistanceSourceM, double walkDistanceTargetM, double distanceNearestPointsM, double maxSpeedMPerS)
        {
            DistanceSourceToTargetM = distance;
            DurationS = duration;
            Id = id;
            TargetStop = target;
            SourceStop = source;
            Route = route;
            IsShapeAvailable=iShapeAvailable;
            SourceNearestLineString = sourceNearestLineString;
            TargetNearestLineString = targetNearestLineString;
            WalkDistanceSourceM=walkDistanceSourceM;
            WalkDistanceTargetM=walkDistanceTargetM;
            DistanceNearestPointsM=distanceNearestPointsM;
            MaxSpeedMPerS=maxSpeedMPerS;
        }
    }
}