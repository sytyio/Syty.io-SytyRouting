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

        public double DistanceSourceToTargetM { get;  }

        public bool IsShapeAvailable{get;}

        public Point? SourceNearestLineString {get;}

        public Point? TargetNearestLineString {get;}

        public double? WalkDistanceSourceM{get;}

        public double? WalkDistanceTargetM{get;}

        public double? DistanceNearestPointsM{get;}



        public override string ToString()
        {
            return "Id = " + Id + " Target = " + TargetStop + "Target on linestring "+TargetNearestLineString+ " Source = " + SourceStop+ "Source on linestring = "+ SourceNearestLineString + " Distance = " + DistanceSourceToTargetM + " meters, Duration = " + DurationS + " seconds" + " Route = "+ Route +" LineString? = "+IsShapeAvailable + " walkSource = " +WalkDistanceSourceM + " walktarget = "+ WalkDistanceTargetM +" DistanceBetween = "+DistanceNearestPointsM;
        }

        public EdgeGtfs(string id, StopGtfs source, StopGtfs target, double distance, double duration, RouteGtfs route, bool iShapeAvailable, Point sourceNearestLineString, Point targetNearestLineString, double walkDistanceSourceM, double walkDistanceTargetM, double distanceNearestPointsM )
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
        }
    }
}