using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class RouteUploaderCOPY : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override async Task UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            var auxiliaryTable=routeTable+Configuration.AuxiliaryTableSuffix;
            var auxiliaryTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            await using var auxTableBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TEMPORARY TABLE IF NOT EXISTS " + auxiliaryTable + " (persona_id INT);"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTablePK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTablePK + " PRIMARY KEY (persona_id);"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;"), // uncomment this line to get a quick view of the routing trajectory on TablePlus. (Modify also resultsBatch and the perm. copy of the aux. table.)
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
            
            await using var resultsBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"), // Only for a quick visual review on TablePlus.
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    new("UPDATE " + routeTable + " r_t SET route = aux_t.computed_route::tgeompoint FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id AND aux_t.is_valid_route = true;"),
                    new("UPDATE " + auxiliaryTable + " SET transport_sequence= coalesce_transport_modes_time_stamps(transport_modes, time_stamps) WHERE is_valid_route = true;"),
                    new("UPDATE " + routeTable + " r_t SET transport_sequence = aux_t.transport_sequence FROM " + auxiliaryTable + " aux_t WHERE  aux_t.persona_id = r_t.id;")
                }
            };
            await using (var reader = await resultsBatch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            ///////////////////////////////////////////////////////////////
            //debug: Aux table copied to a permanent table for benchmarking
            var comparisonTable=auxiliaryTable+"_comp";
            var comparisonTablePK=auxiliaryTablePK + "_comp";;

            bool comparisonTableExists = false;
            await using (var prmCommand = new NpgsqlCommand("SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename  = '" + comparisonTable + "');", connection))
            await using (var reader = await prmCommand.ExecuteReaderAsync())
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
                        new("CREATE TABLE " + comparisonTable + " as (SELECT * FROM " + auxiliaryTable + ");"),
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
                INSERT INTO " + comparisonTable + @" (
                       persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route)
                SELECT persona_id, computed_route, transport_modes, time_stamps, transport_sequence, computed_route_2d, is_valid_route FROM " + auxiliaryTable + @" ON CONFLICT (persona_id) DO
                UPDATE SET 
                 computed_route = EXCLUDED.computed_route,
                 transport_modes = EXCLUDED.transport_modes,
                 time_stamps = EXCLUDED.time_stamps,
                 transport_sequence = EXCLUDED.transport_sequence,
                 computed_route_2d = EXCLUDED.computed_route_2d,
                 is_valid_route = EXCLUDED.is_valid_route;", connection);  
                
                await cmd_insert.ExecuteNonQueryAsync();
            }            
            //:gudeb

  
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
            logger.Info("   Route uploading execution time :: {0}", totalTime);
            logger.Info("uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu");
        }
    }
}