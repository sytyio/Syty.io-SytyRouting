using NetTopologySuite.Geometries;
//using SytyRouting.Model;

namespace SytyRouting.Routing
{
    public interface IRouter
    {
        byte[] ValidateTransportSequence(int id, Point homeLocation, Point workLocation, string[] transportSequence);
    }
}