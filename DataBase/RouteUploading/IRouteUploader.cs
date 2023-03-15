using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public interface IRouteUploader
    {        
        public Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas);
        public Task<int> UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas);
        public Task<int> UploadRouteAsync(string connectionString, string auxiliaryTable, string routeTable, Persona personas);
    }
}