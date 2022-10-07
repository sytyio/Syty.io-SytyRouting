using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;


namespace SytyRouting.Gtfs.ModelCsv
{
    // [Name("route_id")]  route_id : attribute's name in csv
    public class RouteCsv
    {

        [Name("route_id")]
        [NotNull]
        public string? Id { get; set; }

        [Name("route_long_name")]
        [Optional]
        public string? LongName { get; set; }

        [Name("route_type")]
        public int Type { get; set; }

        [Name("agency_id")]
        [NotNull]
        [Optional]
        public string? AgencyId{get;set;}

        public override string ToString()
        {
            return "Id: " + Id + " Name : " + LongName + " Type : " + Type + "AgencyId = "+ AgencyId;
        }
    }
}
