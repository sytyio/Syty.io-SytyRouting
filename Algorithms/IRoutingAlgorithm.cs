using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public interface IRoutingAlgorithm
    {
        List<Node> GetRoute(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitud);
        List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId);
    }
}