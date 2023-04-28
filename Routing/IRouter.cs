using SytyRouting.Algorithms;
using SytyRouting.Model;
using SytyRouting.DataBase;
using NetTopologySuite.Geometries;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        void Initialize(Graph graph, string connectionString, string routeTable, string comparisonTable = "", string benchmarkTable = "");
        void Reset();
        Task StartRouting<A,D,U>() where A: IRoutingAlgorithm, new() where D: IPersonaDownloader, new() where U: IRouteUploader, new();
        List<Persona> GetPersonas();
        int GetComputedRoutesCount();
        TimeSpan GetRoutingTime();
        TimeSpan GetUploadingTime();
        TimeSpan GetDownloadingTime();
        TimeSpan GetExecutionTime();
    }
}