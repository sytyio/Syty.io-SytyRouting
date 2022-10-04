
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
            Id=id;
            Name=name;
            Lat=lat;
            Lon=lon;
        }
    }
}