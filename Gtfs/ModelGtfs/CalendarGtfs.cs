namespace SytyRouting.Gtfs.ModelGtfs
{
    public class CalendarGtfs
    {
        public string ServiceId { get; set; }

        public bool[] Days { get; set; }

        public override string ToString()
        {
            return "ServiceId: " + ServiceId + "Jours " + Days;
        }

            public CalendarGtfs(string serviceId, bool[] days){
            this.ServiceId=serviceId;
            this.Days=days;
        }
    }
}
