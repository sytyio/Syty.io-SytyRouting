using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BaseRouteUploader : IRouteUploader
    {
        public virtual Task UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas)
        {
           throw new NotImplementedException();
        }
    }
}