namespace SytyRouting
{
    [Flags]
    public enum TransportMode
    {
        None = 0,
        Foot = 1,
        Bicycle = 2,
        Car = 4,
        Bus = 8,
        Tram = 16,
        Train = 32,
    }

    public enum TagId
    {
        CyclewayLane = 201,
        CyclewayOpposite = 204,
        CyclewayOppositeLane = 203,
        CyclewayTrack = 202,
        HighwayBridleway = 120,
        HighwayBusGuideway = 116,
        HighwayByway = 121,
        HighwayCycleway = 118,
        HighwayFootway = 119,
        HighwayLivingStreet = 111,
        HighwayMotorway = 101,
        HighwayMotorwayJunction = 103,
        HighwayMotorwayLink = 102,
        HighwayPath = 117,
        HighwayPedestrian = 114,
        HighwayPrimary = 106,
        HighwayPrimaryLink = 107,
        HighwayResidential = 110,
        HighwayRoad = 100,
        HighwaySecondary = 108,
        HighwaySecondaryLink = 124,
        HighwayService = 112,
        HighwayServices = 115,
        HighwaySteps = 122,
        HighwayTertiary = 109,
        HighwayTertiaryLink = 125,
        HighwayTrack = 113,
        HighwayTrunk = 104,
        HighwayTrunkLink = 105,
        HighwayUnclassified = 123,
        JunctionRoundabout = 401,
        TrackTypeGrade1 = 301,
        TrackTypeGrade2 = 302,
        TrackTypeGrade3 = 303,
        TrackTypeGrade4 = 304,
        TrackTypeGrade5 = 305
    }

    public enum StepDirection
    {
        Backward = -1,
        Forward = 1,
    }
    
    public enum OneWayState
    {
        Reversed = -1,
        Unknown = 0,
        Yes = 1,
        No = 2,
    }

    public static class Constants
    {
        public const string ConnectionString = "Host=compute.syty.io;Port=1234;Username=postgres;Password=test123;Database=sytyrun";
        public const string LocalConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=;Database=sytyrun_local";
    }
}