using CsvHelper.Configuration.Attributes;
using System.Diagnostics.CodeAnalysis;

namespace SytyRouting.Gtfs.ModelCsv
{ 
public class AgencyCsv
{

    [Name("agency_id")]
    [NotNull]
    [Optional]
    public string? Id { get; set; }

    [Name("agency_name")]
    [NotNull]
    public string? Name { get; set; }

    [Name("agency_url")]
    [NotNull]
    public string? Url { get; set; }

    public override string ToString()
    {
        return "Id: " + Id + " Name : " + Name + " Url : " + Url;
    }
}
}