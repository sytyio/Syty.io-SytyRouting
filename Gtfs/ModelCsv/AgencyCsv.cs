using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelCsv
{ 
public class AgencyCsv
{

    [Name("agency_id")]
    public string? Id { get; set; }

    [Name("agency_name")]
    public string? Name { get; set; }

    [Name("agency_url")]
    public string? Url { get; set; }

    public override string ToString()
    {
        return "Id: " + Id + " Name : " + Name + " Url : " + Url;
    }
}
}