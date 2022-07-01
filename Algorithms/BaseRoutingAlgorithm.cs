using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public abstract class BaseRoutingAlgorithm : IRoutingAlgorithm
    {
        protected Graph _graph;
        protected List<Node> route = new List<Node>();

        public BaseRoutingAlgorithm(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(double x1, double y1, double x2, double y2)
        {           
            var originNode = _graph.GetNodeByLatitudeLongitude(x1, y1);
            var destinationNode = _graph.GetNodeByLatitudeLongitude(x2, y2);
            
            return RouteSearch(originNode, destinationNode);
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            Node originNode;
            try
            {
                originNode = _graph.GetNodeByOsmId(originNodeOsmId);
            }
            catch (ArgumentException)
            {
                throw new Exception("Unknown value for node osm id.");
            }

            Node destinationNode;
            try
            {
                destinationNode = _graph.GetNodeByOsmId(destinationNodeOsmId);
            }
            catch (ArgumentException)
            {
                throw new Exception("Unknown value for node osm id.");
            }

            return RouteSearch(originNode, destinationNode);
        }

        // Routing lgorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination)
        {
            throw new NotImplementedException();
        }
    }
}