namespace SytyRouting.Gtfs.ModelGtfs
{
    public class TripGtfs
    {

        public RouteGtfs Route { get; set; }

        public string Id { get; set; }

        public ShapeGtfs? Shape { get; set; }

        public ScheduleGtfs? Schedule { get; set; }

        public override string ToString()
        {
            return "Trip id: " + Id + " Route : " + Route + " Shape : " + Shape + "Schedule =" + Schedule;
        }

        // Add shape if there is one
        public TripGtfs(RouteGtfs route, string id, ShapeGtfs shape, ScheduleGtfs schedule){ //ScheduleGtfs2 schedule
            this.Route=route;
            this.Id=id;
            this.Shape=shape;
            this.Schedule=schedule;
        }


    }
}