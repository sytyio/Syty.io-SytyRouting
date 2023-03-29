namespace SytyRouting
{
    public enum GTFSDownloadState
    {
        Completed =     1,
        Error =         0,                
    }

    [Flags]
    public enum CostCriteria
    {
        MinimalTravelDistance =     1,
        MinimalTravelTime =         2,                
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
        public static DateTime BaseDateTime = DateTime.Parse("1970-01-01T00:00:00.0000000+01:00"); //Time Zone: Brussels +1
    }
}