using System.Diagnostics.CodeAnalysis;
namespace SytyRouting.Gtfs.ModelGtfs
{
    public class ScheduleGtfs
    {
        public string Trip { get; set; }

        public Dictionary<int, StopTimesGtfs> Details { get; set; }

        public override string ToString()
        {
            return "Trip = " + Trip + "Nb of stoptimes = " + Details.Count;
        }

        public ScheduleGtfs(string trip, Dictionary<int, StopTimesGtfs> details)
        {
            Trip = trip;
            Details = details;
        }
    }
}