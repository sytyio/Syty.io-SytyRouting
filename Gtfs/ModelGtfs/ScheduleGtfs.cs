namespace SytyRouting.Gtfs.ModelGtfs{



    public class ScheduleGtfs{

        public StopGtfs Stop { get; set; }

        public TimeSpan? ArrivalTime { get; set; }

        public TimeSpan? DepartureTime { get; set; }

        public ScheduleGtfs(StopGtfs stop, TimeSpan arrivalTime, TimeSpan departureTime){
            this.ArrivalTime=arrivalTime;
            this.DepartureTime=departureTime;
            this.Stop=stop;
        }
    }
}