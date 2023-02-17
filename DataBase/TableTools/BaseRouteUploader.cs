// using System.Diagnostics.CodeAnalysis;
// using NLog;
// using NetTopologySuite.Geometries;
// using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BaseRouteUploader : IRouteUploader
    {
        
        // Route upload algorithm implementation
        public virtual Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas)
        {
           throw new NotImplementedException();
        }
    }
}