using CsvHelper.Configuration.Attributes;
namespace SytyRouting.Gtfs.ModelCsv
{
    public class StopCsv
    {

        [Name("stop_id")]
        public string Id { get; set; }

        [Name("stop_name")]
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