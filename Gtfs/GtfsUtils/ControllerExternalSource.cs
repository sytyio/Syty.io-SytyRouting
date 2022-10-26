using SytyRouting.Model;
namespace SytyRouting.Gtfs.GtfsUtils
{
    interface ControllerExternalSource
    {
        Task InitController();
        IEnumerable<Node> GetNodes();
        IEnumerable<Edge> GetEdges();
    }
}