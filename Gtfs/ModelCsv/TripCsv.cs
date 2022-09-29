using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelCsv
{
    public class TripCsv
    {

        [Name("route_id")]
        public string? RouteId { get; set; }

        [Name("trip_id")]
        public string? Id { get; set; }

        [Name("shape_id")]
        [Optional]
        public string? ShapeId { get; set; }

        public override string ToString()
        {
            return "Trip id: " + Id + " Route id : " + RouteId + " Shape id : " + ShapeId;
        }
    }
}