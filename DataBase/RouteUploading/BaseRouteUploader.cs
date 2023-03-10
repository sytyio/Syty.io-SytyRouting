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

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    new("UPDATE " + routeTable + " r_t SET route = aux_t.computed_route::tgeompoint FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id AND aux_t.is_valid_route = true;")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            timeIncrement = stopWatch.Elapsed-timeIncrement;
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            logger.Info("    tgeompoint result propagation time :: {0}", Helper.FormatElapsedTime(timeIncrement));
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            //PLGSQL: Iterates over each transport mode transition to create the corresponding temporal text type sequence (ttext(Sequence)) for each valid route
            var iterationString = @"
            DO 
            $$
            DECLARE
            _id int;
            _arr_tm text[];
            _arr_ts timestamptz[];
            BEGIN    
                FOR _id, _arr_tm, _arr_ts in SELECT persona_id, transport_modes, time_stamps FROM " + auxiliaryTable + @" ORDER BY persona_id ASC
                LOOP
                    UPDATE " + routeTable + @" r_t SET transport_sequence = coalesce_transport_modes_time_stamps(_arr_tm, _arr_ts) FROM " + auxiliaryTable + @" aux_t WHERE aux_t.is_valid_route = true AND r_t.id = _id;
                END LOOP;
            END;
            $$;
            ";

            await using (var cmd = new NpgsqlCommand(iterationString, connection))
            {
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch(Exception e)
                {
                    logger.Debug("!!!!!!!!!!!!!!!!");
                    logger.Debug(" Database error ");
                    logger.Debug("!!!!!!!!!!!!!!!!");
                    logger.Debug(" Unable to compute transport mode transitions: {0}", e.Message);
                    uploadFails++;
                }                
            }

            timeIncrement = stopWatch.Elapsed-timeIncrement;
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            logger.Info("    ttext(Sequence) result propagation time :: {0}", Helper.FormatElapsedTime(timeIncrement));
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            stopWatch.Stop();
            var totalTime = stopWatch.Elapsed;
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            logger.Info("    Result propagation time :: {0}", Helper.FormatElapsedTime(totalTime));
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            return uploadFails;
        }

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