using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;


namespace SytyRouting.Gtfs.ModelCsv
{
    public class CalendarCsv
    {

        [Name("service_id")]
        [NotNull]
        public string? ServiceId { get; set; }

        [Name("monday")]
        public int Monday { get; set; }

        [Name("tuesday")]
        public int Tuesday { get; set; }

        [Name("wednesday")]
        public int Wednesday { get; set; }

        [Name("thursday")]
        public int Thursday { get; set; }

        [Name("friday")]
        public int Friday { get; set; }

        [Name("saturday")]
        public int Saturday { get; set; }

        [Name("sunday")]
        public int Sunday { get; set; }


        public override string ToString()
        {
            return "ServiceId: " + ServiceId + "|| M : " + Monday + "|| T : " + Tuesday + "|| W : " + Wednesday + "|| T : " + Thursday + "|| F : " + Friday + "|| S : " + Saturday + "|| S : " + Sunday;
        }
    }
}