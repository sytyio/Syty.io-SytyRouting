using NetTopologySuite.Geometries;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public interface IRouteUploaderS
    {        
        public Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas);
        public Task<int> UploadRouteAsync(string connectionString, string auxiliaryTable, string routeTable, Persona personas);
    }
}