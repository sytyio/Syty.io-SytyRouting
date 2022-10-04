namespace SytyRouting.Gtfs.ModelGtfs
{
    public class CalendarGtfs
    {
        public string ServiceId { get; set; }

        public bool[] Days { get; set; }

        public override string ToString()
        {
            return "ServiceId: " + ServiceId + "Days " + Days;
        }

        public CalendarGtfs(string serviceId, bool[] days)
        {
            ServiceId = serviceId;
            Days = days;
        }
    }
}
