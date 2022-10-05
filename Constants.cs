namespace SytyRouting
{
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