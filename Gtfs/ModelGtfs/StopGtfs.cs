using SytyRouting.Model;
namespace SytyRouting.Gtfs.ModelGtfs
{
    public class StopGtfs : Node
    {

        public string Id { get; set; }
        public Dictionary<string,EdgeGtfs> InwardEdgesGtfs = new Dictionary<string, EdgeGtfs>();
        public Dictionary<string,EdgeGtfs> OutwardEdgesGtfs = new Dictionary<string,EdgeGtfs>();


        public string? Name { get; set; }

        // public override string ToString()
        // {
        //     return "IdX= "+ Idx + " Internal Id = " + Id + " Name = " + Name + " Lat = " + Y + " Lon = " + X + " Source?= " + ValidSource + " Target?= " + ValidTarget;
        // }

                public override string ToString()
        {
            return "Internal Id = " + Id + " Name = " + Name + " Pos = " + Y.ToString().Replace(",",".")+" "+ X.ToString().Replace(",",".");
        }

        public StopGtfs(string id, string? name, double lat, double lon)
        {
            Id = id;
            Name = name;
            X = lon;
            Y = lat;
            ValidSource = false;
            ValidTarget = false;
            OsmID=long.MaxValue;
        }
    }
}