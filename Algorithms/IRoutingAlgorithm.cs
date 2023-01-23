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
        LineString NodeRouteToLineStringMMilliseconds(List<Node> route, TimeSpan initialTimeStamp);
        LineString OriginToDestinationLineString(double x1, double y1, double x2, double y2, byte transportMode, TimeSpan initialTimeStamp);
        Dictionary<int, Tuple<byte,int>> SingleTransportModeTransition(Node origin, Node destination, byte transportMode);

        double GetRouteCost();
    }
}