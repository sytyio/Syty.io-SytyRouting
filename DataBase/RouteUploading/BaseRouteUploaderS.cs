using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BaseRouteUploaderS : IRouteUploaderS
    {
        public virtual Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas)
        {
           throw new NotImplementedException();
        }

        public virtual Task<int> UploadRouteAsync(string connectionString, string auxiliaryTable, string routeTable, Persona personas)
        {
           throw new NotImplementedException();
        }
    }
}