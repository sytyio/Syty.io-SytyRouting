using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public interface IRoutingAlgorithm
    {
        void Initialize(Graph graph);
        List<Node> GetRoute(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitud, byte[] transportModesSequence);
        List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId, byte[] transportModesSequence);
        Dictionary<int,Tuple<byte,int>> GetTransportModeTransitions();
        LineString ConvertRouteFromNodesToLineString(List<Node> route, TimeSpan initialTimeStamp);
        double GetRouteCost();
    }
}