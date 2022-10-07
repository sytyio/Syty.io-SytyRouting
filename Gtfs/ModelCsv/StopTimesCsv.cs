using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;


namespace SytyRouting.Gtfs.ModelCsv
{
    public class StopTimesCsv
    {
        [Name("trip_id")]
        [NotNull]
        public string? TripId { get; set; }

        [Name("arrival_time")]
        [NotNull]
        public string? ArrivalTime { get; set; }

        [Name("departure_time")]
        [NotNull]
        public string? DepartureTime { get; set; }

        [Name("stop_id")]
        [NotNull]
        public string? StopId { get; set; }

        [Name("stop_sequence")]
        public int Sequence { get; set; }


        public override string ToString()
        {
            return "TripId = " + TripId + " ArrivalTime = " + ArrivalTime + " DepartureTime = " + DepartureTime + " StopId = " + StopId + "Stop Sequence = " + Sequence;
        }
    }
}