using SytyRouting.Algorithms;
using SytyRouting.Model;
using SytyRouting.DataBase;
using NetTopologySuite.Geometries;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        void Initialize(Graph graph, string connectionString, string routeTable, string auxiliaryTable);
        Task StartRouting<A,U>() where A: IRoutingAlgorithm, new() where U: IRouteUploader, new();
        List<Persona> GetPersonas();
        int GetComputedRoutesCount();
        TimeSpan GetRoutingTime();
        TimeSpan GetUploadingTime();
    }
}