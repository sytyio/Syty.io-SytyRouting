namespace SytyRouting
{
    // [Flags]
    // public enum TransportMode
    // {
    //     None =        0,
    //     Foot =        1,
    //     Bicycle =     2,
    //     Horse =       4,
    //     Car =         8,
    //     Bus =        16,
    //     Tram =       32,
    //     Train =      64,
    //     Undefined = 128,
    // }

    // Database: compute.syty.io
    // OSM Tags reference: https://wiki.openstreetmap.org/wiki/Tags
    // * main transport mode
    // public enum OSMTagId
    // {
    //     HighwayRoad = 100,             // Undefined. (OSM: This tag is intentionally vague. Treat it as an error.)
    //
    //     HighwayMotorway = 101,         // Car, Bus    
    //     HighwayMotorwayLink = 102,     // Car, Bus. (Motorway on- off-ramps.)
    //     HighwayMotorwayJunction = 103, // Car, Bus. (Motorway exit.)
    //     HighwayTrunk = 104,            // Car, Bus
    //     HighwayTrunkLink = 105,        // Car, Bus. (Slip roads/ramps connecting a Trunk Highway to other roadways.)
    //     HighwayPrimary = 106,          // Car, Bus
    //     HighwayPrimaryLink = 107,      // Car, Bus. (Slip roads/ramps connecting a Primary Highway to minor roadways.)
    //     HighwaySecondary = 108,        // Car, Bus
    //     HighwayTertiary = 109,         // Car, Bus
    //     HighwayResidential = 110,      // Foot, Bicycle, Car, Bus
    //     HighwayLivingStreet = 111,     // Foot, Bicycle, Car
    //     HighwayService = 112,          // Foot, Bicycle, Car, Bus
    //     HighwayTrack = 113,            // Undefined. (OSM: used for agriculture, forestry, outdoor recreation, and similar activities on open land.)
    //     HighwayPedestrian = 114,       // Foot
    //     HighwayServices = 115,         // Car, Bus. (Motorway rest/service area.)
    //     HighwayBusGuideway = 116,      // Bus
    //     HighwayPath = 117,             // Foot, Bicycle
    //     HighwayCycleway = 118,         // Foot, Bicycle*
    //     HighwayFootway = 119,          // Foot*, Bicycle
    //     HighwayBridleway = 120,        // Foot, Bicycle, Horse*
    //     HighwayByway = 121,            // Undefined. (OSM: Using this tag is discouraged, use highway=track/path + designation + access instead.)
    //     HighwaySteps = 122,            // Foot. (Flights of steps on footways and paths.)
    //     HighwayUnclassified = 123,     // Bicycle, Car. (OSM: Minor public roads, less important than Tertiary roads.)
    //     HighwaySecondaryLink = 124,    // Car, Bus. (Slip roads/ramps connecting a Secondary Highway to minor roadways.)
    //     HighwayTertiaryLink = 125,     // Car, Bus. (Slip roads/ramps connecting a Tertiary Highway to minor roadways.)
    //    
    //     CyclewayLane = 201,            // Bicycle
    //     CyclewayTrack = 202,           // Bicycle
    //     CyclewayOppositeLane = 203,    // Bicycle
    //     CyclewayOpposite = 204,        // Bicycle
    //    
    //     TrackTypeGrade1 = 301,         // Undefined. (OSM: Solid. Usually a paved surface.)
    //     TrackTypeGrade2 = 302,         // Undefined. (OSM: Mostly solid. Usually an unpaved track.)
    //     TrackTypeGrade3 = 303,         // Undefined. (OSM: Neither solid nor soft? An unpaved track.)
    //     TrackTypeGrade4 = 304,         // Undefined. (OSM: Mostly soft. An unpaved track.)
    //     TrackTypeGrade5 = 305,         // Undefined. (OSM: Soft. An unimproved track.)
    //
    //     JunctionRoundabout = 401,      // Car, Bus
    // }

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
        public const string DefaulTransportMode = "None";
        public const int MaxNumberOfTransportModes = sizeof(ushort) * 8; // Number of bits to be used in the TransportModes masks
    }
}