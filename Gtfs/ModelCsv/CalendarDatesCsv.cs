using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;


namespace SytyRouting.Gtfs.ModelCsv
{
    public class CalendarDateCsv
    {

        [Name("service_id")]
        [NotNull]
        public string? ServiceId { get; set; }

        [Name("date")]
        [NotNull]
        public string? DateException {get;set;}

        [Name("exception_type")]
        public int ExceptionType {get;set;}


        public override string ToString()
        {
            return "ServiceId: " + ServiceId + " Date exception " + DateException + " Type of exception "+ExceptionType;
        }
    }
}