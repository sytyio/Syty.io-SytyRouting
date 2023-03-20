using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public interface IRouteUploader
    {        
        public Task UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas);
    }
}