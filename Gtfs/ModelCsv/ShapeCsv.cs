using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration.Attributes;

namespace SytyRouting.Gtfs.ModelCsv
{
    public class ShapeCsv
    {

        [Name("shape_id")]
        [NotNull]
        public string? Id { get; set; }

        [Name("shape_pt_lat")]
        public double PtLat { get; set; }

        [Name("shape_pt_lon")]
        public double PtLon { get; set; }

        [Name("shape_pt_sequence")]
        public int PtSequence { get; set; }

        public override string ToString()
        {
            return "Id = " + Id + " Lat = " + PtLat + " Lon = " + PtLon + " Pos in sequence = " + PtSequence;
        }
    }
}
