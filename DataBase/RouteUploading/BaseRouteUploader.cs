using System.Diagnostics;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NLog;
using Npgsql;
using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public abstract class BaseRouteUploader : IRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        protected async Task<int> PropagateResultsAsync(string connectionString, string auxiliaryTable, string routeTable)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var timeIncrement = stopWatch.Elapsed;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;

            //PLGSQL: Iterates over each transport mode transition to create the corresponding temporal text type sequence (ttext(Sequence)) for each valid route
            var iterationString = @"
            DO 
            $$
            DECLARE
            _id int;
            _arr_tm text[];
            _arr_ts timestamptz[];
            BEGIN    
                FOR _id, _arr_tm, _arr_ts in SELECT persona_id, transport_modes, time_stamps FROM " + auxiliaryTable + @"
                LOOP
                    UPDATE " + auxiliaryTable + @" SET transport_sequence= coalesce_transport_modes_time_stamps(_arr_tm, _arr_ts) WHERE is_valid_route = true AND persona_id = _id;
                END LOOP;
            END;
            $$;
            ";

            var updateString = "UPDATE " + routeTable + " r_t SET transport_sequence = aux_t.transport_sequence FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id;";

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    new("UPDATE " + routeTable + " r_t SET route = aux_t.computed_route::tgeompoint FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id AND aux_t.is_valid_route = true;"),
                    new(iterationString),
                    new(updateString)
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            stopWatch.Stop();
            var totalTime = stopWatch.Elapsed;
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            logger.Info("    Result propagation time :: {0}", Helper.FormatElapsedTime(totalTime));
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");


            return uploadFails;
        }

        // public virtual Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas)
        // {
        //    //throw new NotImplementedException();
        // }

        public virtual Task<int> UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas)
        {
           throw new NotImplementedException();
        }

        public virtual Task<int> UploadRouteAsync(string connectionString, string auxiliaryTable, string routeTable, Persona personas)
        {
           throw new NotImplementedException();
        }
    }
}