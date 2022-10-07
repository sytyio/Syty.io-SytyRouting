namespace SytyRouting.Gtfs.ModelGtfs
{
    public class StopGtfs
    {

        public string Id { get; set; }

        public string? Name { get; set; }

        public double Lat { get; set; }

        public double Lon { get; set; }

        public bool ValidTarget {get;set;}
        public bool ValidSource {get;set;}

        public List<EdgeGtfs> InwardEdges = new List<EdgeGtfs>();
        public List<EdgeGtfs> OutwardEdges = new List<EdgeGtfs>();

        public override string ToString()
        {
            return "Id = " + Id  + " Name = " + Name + " Lat = " + Lat + " Lon = " + Lon + " Source?= " + ValidSource + " Target?= "+ValidTarget;
        }

        public StopGtfs(string id, string? name, double lat, double lon){
            Id=id;
            Name=name;
            Lat=lat;
            Lon=lon;
            ValidSource=false;
            ValidTarget=false;
        }
    }
}