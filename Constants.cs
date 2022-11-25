namespace SytyRouting
{
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
       
    }
}