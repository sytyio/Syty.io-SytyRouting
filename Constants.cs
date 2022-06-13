namespace SytyRouting
{
    public static class Constants
    {
        public const string connectionString = "Host=compute.syty.io:1234;Username=postgres;Password=test123;Database=sytyrun";

        public enum OneWayState
        {
            Reversed = -1,
            Unknown = 0,
            Yes = 1,
            No = 2,
        }
    }
}