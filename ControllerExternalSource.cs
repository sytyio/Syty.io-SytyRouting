namespace SytyRouting.Model
{
    interface ControllerExternalSource
    {
        Task InitController();
        IEnumerable<Node> GetNodes();
        IEnumerable<Edge> GetEdges();

        IEnumerable<Node> GetInternalNodes();
    }
}