namespace SytyRouting.Gtfs.ModelGtfs
{
    public class CalendarGtfs
    {
        public string ServiceId { get; set; }

        public List<DateTime> Dates {get;set;}

        public DateTime DateBegin {get;set;}

        public DateTime DateEnd {get;set;}
        public bool[] Days { get; set; }

        public override string ToString()
        {
            return "ServiceId: " + ServiceId + "Days " + Days;
        }

        public CalendarGtfs(string serviceId, bool[] days, List<DateTime> dates, DateTime dateBegin, DateTime dateEnd)
        {
            ServiceId = serviceId;
            Days = days;
            Dates = dates;
            DateBegin= dateBegin;
            DateEnd = dateEnd;
        }
    }
}
