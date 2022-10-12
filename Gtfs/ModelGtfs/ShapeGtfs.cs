using NetTopologySuite.Geometries;

namespace SytyRouting.Gtfs.ModelGtfs
{
    public class ShapeGtfs
    {

        public string Id { get; set; }

        public Dictionary<int,Point> ItineraryPoints {get;set;}

        public LineString LineString {get;set;}

        public double[]? ArrayDistances; 

        public List<LineString>? SplitLineString;

        public override string ToString()
        {
            return "Id = " + Id + " Nb points = " + ItineraryPoints.Count;
        }

        public ShapeGtfs(string id, Dictionary<int,Point> itineraryPoints, LineString lineString){
            Id=id;
            ItineraryPoints=itineraryPoints;
            LineString=lineString;
        }
    }
}
