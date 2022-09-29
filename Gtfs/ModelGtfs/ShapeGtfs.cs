using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using NetTopologySuite.Geometries;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class ShapeGtfs
    {

        public string Id { get; set; }

        public Dictionary<int,Point> ItineraryPoints {get;set;}

        public LineString LineString {get;set;}

        public override string ToString()
        {
            return "Id = " + Id + " Nb points = " + ItineraryPoints.Count + " LineString = "+LineString;
        }

        public ShapeGtfs(string id, Dictionary<int,Point> itineraryPoints, LineString lineString){
            this.Id=id;
            this.ItineraryPoints=itineraryPoints;
            this.LineString=lineString;
        }
    }
}
