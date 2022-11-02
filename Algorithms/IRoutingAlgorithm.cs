using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public interface IRoutingAlgorithm
    {
        void Initialize(Graph graph);
        List<Node> GetRoute(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitud, byte transportMode);
        List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId, byte transportMode);
        LineString ConvertRouteFromNodesToLineString(List<Node> route, TimeSpan initialTimeStamp);
        double GetRouteCost();
    }
}