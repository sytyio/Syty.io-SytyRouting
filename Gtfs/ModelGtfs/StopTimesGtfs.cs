namespace SytyRouting.Gtfs.ModelGtfs{

    public class StopTimesGtfs{

        public StopGtfs Stop { get; set; }

        public TimeSpan ArrivalTime { get; set; }

        public TimeSpan DepartureTime { get; set; }

        public int Sequence { get; set; }

        public override string ToString()
        {
            return "Stop "+ Stop + " Arrival = "+ ArrivalTime + " Departure = " + DepartureTime;
        }

        public StopTimesGtfs(StopGtfs stop, TimeSpan arrivalTime, TimeSpan departureTime, int sequence){
            ArrivalTime=arrivalTime;
            DepartureTime=departureTime;
            Stop=stop;
            Sequence=sequence;
        }
    }
}