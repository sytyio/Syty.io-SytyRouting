using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class RouteUploader : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override async Task<int> UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;


            /////////////////

            var auxiliaryTable=routeTable+Configuration.AuxiliaryTableSuffix;
            var auxiliaryTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;
            var auxiliaryTableFK=auxiliaryTable+Configuration.FKConstraintSuffix;

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            await using var auxTableBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TEMPORARY TABLE IF NOT EXISTS " + auxiliaryTable + " (persona_id INT);"),
                    //new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTableFK + ";"),
                    //new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTableFK + " FOREIGN KEY (persona_id) REFERENCES " + routeTable + " (id);"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTablePK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTablePK + " PRIMARY KEY (persona_id);"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS is_valid_route BOOL;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_modes TEXT[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS time_stamps TIMESTAMPTZ[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);")
                }
            };

            await using (var reader = await auxTableBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table creation",auxiliaryTable);
            }

            /////////////////

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
            await importer.CloseAsync();

            /////////////////
            
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

            await using var resultsBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"), // Only for a quick visual review on TablePlus. 
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    new("UPDATE " + routeTable + " r_t SET route = aux_t.computed_route::tgeompoint FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id AND aux_t.is_valid_route = true;"),
                    new(iterationString),
                    new(updateString)
                }
            };

            await using (var reader = await resultsBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            /////////////////

            //debug: Aux table copied to a permanent table for benchmarking
            var comparisonTable=auxiliaryTable+"_comp";
            var comparisonTablePK=auxiliaryTablePK + "_comp";;

            // await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + auxiliaryTable + "_comp;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            bool comparisonTableExists = false;
            await using (var command = new NpgsqlCommand("SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename  = '" + comparisonTable + "');", connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    comparisonTableExists = Convert.ToBoolean(reader.GetValue(0));
                }
            }

            if(!comparisonTableExists)
            {
                await using var compTableBatch = new NpgsqlBatch(connection)
                {
                    BatchCommands =
                    {
                        //new("CREATE TABLE IF NOT EXISTS " + auxiliaryTable + " (persona_id INT);"),
                        new("CREATE TABLE " + comparisonTable + " as (SELECT * FROM " + auxiliaryTable + ");"),
                        //new("ALTER TABLE " + comparisonTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTablePK + ";"),
                        new("ALTER TABLE " + comparisonTable + " ADD CONSTRAINT " + auxiliaryTablePK + " PRIMARY KEY (persona_id);")
                    }
                };

                await using (await compTableBatch.ExecuteReaderAsync())
                {
                    logger.Debug("{0} table creation",comparisonTable);
                }

            }
            else
            {
                await using var cmd_insert = new NpgsqlCommand(@"
                INSERT INTO " + comparisonTable + @" (persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route)
                SELECT persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route FROM " + auxiliaryTable + @" ON CONFLICT (persona_id) DO
                UPDATE SET 
                 computed_route = EXCLUDED.computed_route,
                 transport_modes = EXCLUDED.transport_modes,
                 time_stamps = EXCLUDED.time_stamps,
                 transport_sequence = EXCLUDED.transport_sequence,
                 computed_route_2d = EXCLUDED.computed_route_2d,
                 is_valid_route = EXCLUDED.is_valid_route;", connection);  

                //SELECT persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route FROM " + auxiliaryTable + ";", connection);
                
                await cmd_insert.ExecuteNonQueryAsync();
            }
        

            
            //


   
            await connection.CloseAsync();

            //await PropagateResultsAsync(connectionString,auxiliaryTable,routeTable);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Info("   Route uploading execution time :: {0}", totalTime);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");

            return uploadFails;
        }
    }
}