using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Text;
namespace SytyRouting.Gtfs.ModelGtfs

{
    public class StopGtfs
    {

        public string Id { get; set; }

        public string Name { get; set; }

        public double Lat { get; set; }

        public double Lon { get; set; }

        public override string ToString()
        {
            return "Id = " + Id  + " Name = " + Name + " Lat = " + Lat + " Lon = " + Lon;
        }

        public StopGtfs(string id, string name, double lat, double lon){
            this.Id=id;
            this.Name=name;
            this.Lat=lat;
            this.Lon=lon;
        }
    }
}