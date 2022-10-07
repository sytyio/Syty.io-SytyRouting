using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;


namespace SytyRouting.Gtfs.ModelCsv
{
    public class StopCsv
    {

        [Name("stop_id")]
        [NotNull]
        public string? Id { get; set; }

        [Name("stop_name")]
        [Optional]
        public string? Name { get; set; }

        [Name("stop_lat")]
        public double Lat { get; set; }

        [Name("stop_lon")]
        public double Lon { get; set; }

        public override string ToString()
        {
            return "Id = " + Id + " Name = " + Name + " Lat = " + Lat + " Lon = " + Lon;
        }
    }
}