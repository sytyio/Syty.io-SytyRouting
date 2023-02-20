using SytyRouting.Algorithms;
using SytyRouting.Model;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        void Initialize(Graph graph, string routeTable, string auxiliaryTable);
        Task StartRouting<T>() where T: IRoutingAlgorithm, new();
        List<Persona> GetPersonas();
        int GetComputedRoutesCount();
    }
}