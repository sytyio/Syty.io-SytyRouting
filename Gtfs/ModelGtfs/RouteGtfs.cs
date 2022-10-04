namespace SytyRouting.Gtfs.ModelGtfs
{
    public class RouteGtfs
    {
        public string Id { get; set; }

        public string? LongName { get; set; }

        public int Type { get; set; }

        public Dictionary<string, TripGtfs>? Trips;

        public override string ToString()
        {
            return "Id: " + Id + " Name : " + LongName + " Nb trips associated = " + Trips.Count + "Type route = "+ Type;
        }

        public RouteGtfs(string id, string name, int type, Dictionary<string, TripGtfs>? trips)
        {
            Id = id;
            LongName = name;
            Type = type;
            Trips = trips;
        }
    }
}
