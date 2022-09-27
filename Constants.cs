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
        // public const string ConnectionString = "Host=compute.syty.io;Port=1234;Username=postgres;Password=test123;Database=sytyrun";
        // public const string LocalConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=;Database=sytyrun_local";
    }
}