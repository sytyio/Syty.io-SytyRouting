using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class RouteGtfs
    {
        public string Id { get; set; }

        public string? LongName { get; set; }

        public int Type { get; set; }

        public Dictionary<string,TripGtfs>? Trips;

        public override string ToString()
        {
            return "Id: " + Id +" Name : " + LongName + " Nb trips associés = "+ Trips.Count ; //" Type : " + Type + 
        }

        public RouteGtfs(string id, string name, int type, Dictionary<string,TripGtfs>? trips){
            this.Id=id;
            this.LongName=name;
            this.Type=type;
            this.Trips=trips;
        }

    }
}
