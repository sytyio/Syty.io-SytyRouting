using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public interface IRoutingAlgorithm
    {
        void Initialize(Graph graph);
        List<Node> GetRoute(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitud);
        List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId);
        List<XYMPoint> ConvertRouteFromNodesToXYMPoints(List<Node> route, TimeSpan initialTimeStamp);
        double GetRouteCost();
    }
}