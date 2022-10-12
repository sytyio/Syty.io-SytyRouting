using NetTopologySuite.Geometries;
using System.Diagnostics.CodeAnalysis;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class TripGtfs
    {

        public RouteGtfs Route { get; set; }

        public string Id { get; set; }

        public ShapeGtfs? Shape { get; set; }

        [NotNull]
        public ScheduleGtfs? Schedule { get; set; }
        
        public CalendarGtfs Service {get;set;}

        public override string ToString()
        {
            return "Trip id: " + Id + " Service Id = "+ Service+ " Route : " + Route + " Shape : " + Shape + "Schedule =" + Schedule;
        }

        public TripGtfs(RouteGtfs route, string id, ShapeGtfs? shape, ScheduleGtfs schedule, CalendarGtfs service){
            Route=route;
            Id=id;
            Shape=shape;
            Schedule=schedule;
            Service=service;
        }
    }
}