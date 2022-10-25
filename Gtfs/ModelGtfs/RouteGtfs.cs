using System.Diagnostics.CodeAnalysis;
namespace SytyRouting.Gtfs.ModelGtfs
{
    public class RouteGtfs
    {
        public string Id { get; set; }

        public string? LongName { get; set; }

        public AgencyGtfs? Agency {get;set;}

        public int Type { get; set; }

        [NotNull]
        public Dictionary<string, TripGtfs>? Trips;

        public override string ToString()
        {
            return "Id: " + Id + " Name : " + LongName + " Nb trips associated = " + Trips.Count + " Type = "+ Type + "Agency = "+Agency;
        }

        public RouteGtfs(string id, string? name, int type, Dictionary<string, TripGtfs> trips, AgencyGtfs? agency)
        {
            Id = id;
            LongName = name;
            Type = type;
            Trips = trips;
            Agency= agency;
        }
    }
}
