using SytyRouting.Algorithms;
using SytyRouting.Model;
using SytyRouting.DataBase;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        void Initialize(Graph graph, string routeTable, string auxiliaryTable);
        Task StartRouting<A,U>() where A: IRoutingAlgorithm, new() where U: IRouteUploader, new();
        List<Persona> GetPersonas();
        int GetComputedRoutesCount();
        TimeSpan GetRoutingTime();
        TimeSpan GetUploadingTime();
        byte[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence);
    }
}