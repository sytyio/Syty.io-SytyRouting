using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class StopTimesGtfs
    {
        public TripGtfs Trip { get; set; }


       public Dictionary<int,ScheduleGtfs> Details { get; set; }


        public override string ToString()
        {
            return "Trip = " + Trip + " Details = "+Details;
        }

        public StopTimesGtfs(TripGtfs trip, Dictionary<int,ScheduleGtfs> details)
        {
            this.Trip=trip;
            this.Details=details;
        }
    }
}