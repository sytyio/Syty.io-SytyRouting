using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms
{
    public abstract class BaseRoutingAlgorithm : IRoutingAlgorithm
    {
        [NotNull]
        protected Graph? _graph;
        protected List<Node> route = new List<Node>();
        protected double routeCost;

        public virtual void Initialize(Graph graph)
        {
            _graph = graph;
        }

        public List<Node> GetRoute(double x1, double y1, double x2, double y2)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
            var originNode = _graph.GetNodeByLatitudeLongitude(x1, y1);
            var destinationNode = _graph.GetNodeByLatitudeLongitude(x2, y2);
            
            return RouteSearch(originNode, destinationNode);
        }

        public List<Node> GetRoute(long originNodeOsmId, long destinationNodeOsmId)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }
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

        public List<Edge> ConvertRouteFromNodesToEdges(List<Node> route)
        {
            if (_graph == null)
            {
                throw new ArgumentException("You must initialize the routing algorithm first!");
            }

            List<Edge> edgeRoute = new List<Edge>(0);
            for(var i = 0; i < route.Count-1; i++)
            {
                var edge = route[i].OutwardEdges.Find(e => e.TargetNode.Idx == route[i+1].Idx);
                if(edge is not null)
                {
                    edgeRoute.Add(edge);
                }
                else
                {
                    throw new Exception("Impossible to find corresponding Outward Edge");
                }
            }

            Console.WriteLine("Edge OsmIDs:");
            foreach(var edge in edgeRoute)
            {
                Console.WriteLine("{0}", edge.OsmID);
            }

            return edgeRoute;
        }

        public double GetRouteCost()
        {
            return routeCost;
        }

        // Routing algorithm implementation
        protected virtual List<Node> RouteSearch(Node origin, Node destination)
        {
            throw new NotImplementedException();
        }
    }
}