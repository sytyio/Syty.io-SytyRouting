using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelCsv
{
    public class StopTimesCsv
    {
        [Name("trip_id")]
        public string? TripId { get; set; }

        [Name("arrival_time")]
        public string? ArrivalTime { get; set; }

        [Name("departure_time")]
        public string? DepartureTime { get; set; }

        [Name("stop_id")]
        public string? StopId { get; set; }

        [Name("stop_sequence")]
        public int Sequence { get; set; }


        public override string ToString()
        {
            return "TripId = " + TripId + " ArrivalTime = " + ArrivalTime + " DepartureTime = " + DepartureTime + " StopId = " + StopId + "Stop Sequence = " + Sequence;
        }
    }
}