using SytyRouting.Model;
namespace SytyRouting.Gtfs.ModelGtfs
{
    public class StopGtfs : Node
    {

        public string Id { get; set; }

        public double X;
        public double Y;

        public bool ValidTarget;

        public bool ValidSource;
        public Dictionary<string,EdgeGtfs> InwardEdgesGtfs = new Dictionary<string, EdgeGtfs>();
        public Dictionary<string,EdgeGtfs> OutwardEdgesGtfs = new Dictionary<string,EdgeGtfs>();

        public string? Name { get; set; }

        public override string ToString()
        {
            return "Id = " + Id + " Name = " + Name + " Lat = " + Y + " Lon = " + X + " Source?= " + ValidSource + " Target?= " + ValidTarget;
        }

        public StopGtfs(string id, string? name, double lat, double lon)
        {
            Id = id;
            Name = name;
            Y = lat;
            X = lon;
            ValidSource = false;
            ValidTarget = false;
        }
    }
}