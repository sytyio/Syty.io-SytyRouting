using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelCsv
{
    // [Name("route_id")]  route_id : attribute's name in csv
    public class RouteCsv
    {

        [Name("route_id")]
        public string? Id { get; set; }

        [Name("route_long_name")]
        public string? LongName { get; set; }

        [Name("route_type")]
        public int Type { get; set; }

        public override string ToString()
        {
            return "Id: " + Id + " Name : " + LongName + " Type : " + Type;
        }
    }
}
