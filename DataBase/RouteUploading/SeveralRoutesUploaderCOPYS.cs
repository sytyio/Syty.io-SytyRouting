using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderCOPYS : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override async Task<int> UploadRoutesAsync(string connectionString, string auxiliaryTable, string routeTable, List<Persona> personas)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;

            using var importer = connection.BeginBinaryImport("COPY " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) FROM STDIN (FORMAT binary)");
            foreach (var persona in personas)
            {
                await importer.StartRowAsync();
                await importer.WriteAsync(persona.Id);
                await importer.WriteAsync(persona.Route);
                await importer.WriteAsync(persona.TTextTransitions.Item1);
                await importer.WriteAsync(persona.TTextTransitions.Item2);
            }

            await importer.CompleteAsync();


   
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Info("   Route uploading execution time :: {0}", totalTime);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");

            return uploadFails;
        }

        public static async Task<int> PropagateResultsSAsync(string connectionString, string auxiliaryTable, string routeTable)
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
                    //new("UPDATE " + auxiliaryTable + " SET route = computed_route::tgeompoint WHERE is_valid_route = true;")
                    new("UPDATE " + routeTable + " rt SET route = t_aux.computed_route::tgeompoint FROM " + auxiliaryTable + " t_aux WHERE  t_aux.persona_id = rt.id AND t_aux.is_valid_route = true;")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            timeIncrement = stopWatch.Elapsed-timeIncrement;
            logger.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            logger.Info("    tgeompoint result propagation time :: {0}", Helper.FormatElapsedTime(timeIncrement));
            logger.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

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
                    RAISE NOTICE 'id: %', _id;
                    RAISE NOTICE 'transport modes: %', _arr_tm;
                    RAISE NOTICE 'time stamps: %', _arr_ts;
                    UPDATE " + auxiliaryTable + @" SET transport_sequence= coalesce_transport_modes_time_stamps(_arr_tm, _arr_ts) WHERE is_valid_route = true AND persona_id = _id;
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
                    logger.Debug(" Unable to compute transport mode transitions on the database: {0}", e.Message);
                    uploadFails++;
                }                
            }

            timeIncrement = stopWatch.Elapsed-timeIncrement;
            logger.Info("------------------------------------------------------------------------------------");
            logger.Info("    ttext(Sequence) calculation time :: {0}", Helper.FormatElapsedTime(timeIncrement));
            logger.Info("------------------------------------------------------------------------------------");

            var updateString = @"
            DO 
            $$
            DECLARE
            _id int;
            _r tgeompoint;
            _ts ttext(Sequence);
            BEGIN    
                FOR _id, _ts in SELECT persona_id, transport_sequence FROM " + auxiliaryTable + @" ORDER BY persona_id ASC
                LOOP
                    RAISE NOTICE 'id: %', _id;
                    RAISE NOTICE 'route: %', _r;
                    RAISE NOTICE 'transport sequence: %', _ts;
                    UPDATE " + routeTable + @" SET transport_sequence = _ts WHERE id = _id;
                END LOOP;
            END;
            $$;
            ";

            await using (var cmd = new NpgsqlCommand(updateString, connection))
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
                    logger.Debug(" Unable to update transport sequences on the database: {0}", e.Message);
                    uploadFails++;
                }                
            }

            timeIncrement = stopWatch.Elapsed-timeIncrement;
            logger.Info("'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''");
            logger.Info("    ttext(Sequence) result propagation time :: {0}", Helper.FormatElapsedTime(timeIncrement));
            logger.Info("'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''");

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            logger.Info("     Result propagation time :: {0}", totalTime);
            logger.Info("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            return uploadFails;
        }
    }
}