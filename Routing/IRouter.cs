using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        Task StartRouting<T>() where T: IRoutingAlgorithm, new();
    }
}