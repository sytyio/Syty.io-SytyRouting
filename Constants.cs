namespace SytyRouting
{
    [Flags]
    public enum TransportMode
    {
        None =        0,
        Foot =        1,
        Bicycle =     2,
        Horse =       4,
        Car =         8,
        Bus =        16,
        Tram =       32,
        Train =      64,
        Undefined = 128,
    }

    // OSM Tags reference: https://wiki.openstreetmap.org/wiki/Tags
    // * main transport mode
    public enum OSMTagId
    {
        CyclewayLane = 201,            // Bicycle
        CyclewayOpposite = 204,        // Bicycle
        CyclewayOppositeLane = 203,    // Bicycle
        CyclewayTrack = 202,           // Bicycle
        HighwayBridleway = 120,        // Foot, Bicycle, Horse*
        HighwayBusGuideway = 116,      // Bus
        HighwayByway = 121,            // Undefined. (OSM: Using this tag is discouraged, use highway=track/path + designation + access instead.)
        HighwayCycleway = 118,         // Foot, Bicycle*
        HighwayFootway = 119,          // Foot*, Bicycle
        HighwayLivingStreet = 111,     // Foot, Bicycle, Car
        HighwayMotorway = 101,         // Car, Bus
        HighwayMotorwayJunction = 103, // Car, Bus. (Motorway exit.)
        HighwayMotorwayLink = 102,     // Car, Bus. (Motorway on- off-ramps.)
        HighwayPath = 117,             // Foot, Bicycle
        HighwayPedestrian = 114,       // Foot
        HighwayPrimary = 106,          // Car, Bus
        HighwayPrimaryLink = 107,      // Car, Bus. (Slip roads/ramps connecting a Primary Highway to minor roadways.)
        HighwayResidential = 110,      // Foot, Bicycle, Car, Bus
        HighwayRoad = 100,             // Undefined. (OSM: This tag is intentionally vague. Treat it as an error.)
        HighwaySecondary = 108,        // Car, Bus
        HighwaySecondaryLink = 124,    // Car, Bus. (Slip roads/ramps connecting a Secondary Highway to minor roadways.)
        HighwayService = 112,          // Foot, Bicycle, Car, Bus
        HighwayServices = 115,         // Car, Bus. (Motorway rest/service area.)
        HighwaySteps = 122,            // Foot. (Flights of steps on footways and paths.)
        HighwayTertiary = 109,         // Car, Bus
        HighwayTertiaryLink = 125,     // Car, Bus. (Slip roads/ramps connecting a Tertiary Highway to minor roadways.)
        HighwayTrack = 113,            // Undefined. (OSM: used for agriculture, forestry, outdoor recreation, and similar activities on open land.)
        HighwayTrunk = 104,            // Car, Bus
        HighwayTrunkLink = 105,        // Car, Bus. (Slip roads/ramps connecting a Trunk Highway to other roadways.)
        HighwayUnclassified = 123,     // Bicycle, Car. (OSM: Minor public roads, less important than Tertiary roads.)
        JunctionRoundabout = 401,      // Car, Bus
        TrackTypeGrade1 = 301,         // Undefined. (OSM: Solid. Usually a paved surface.)
        TrackTypeGrade2 = 302,         // Undefined. (OSM: Mostly solid. Usually an unpaved track.)
        TrackTypeGrade3 = 303,         // Undefined. (OSM: Neither solid nor soft? An unpaved track.)
        TrackTypeGrade4 = 304,         // Undefined. (OSM: Mostly soft. An unpaved track.)
        TrackTypeGrade5 = 305          // Undefined. (OSM: Soft. An unimproved track.)
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