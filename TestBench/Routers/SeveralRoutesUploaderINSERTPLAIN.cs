using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderINSERTPLAIN : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override async Task<int> UploadRoutesAsync(string connectionString, string routeTable, List<Persona> personas, string comparisonTable = "", string benchmarkingTable = "")
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;        

            /////////////////

            var auxiliaryTable=routeTable+Configuration.AuxiliaryTableSuffix;
            var auxiliaryTableTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;
            var auxiliaryTableTableFK=auxiliaryTable+Configuration.FKConstraintSuffix;

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();

            await using var auxTableBatch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TEMPORARY TABLE IF NOT EXISTS " + auxiliaryTable + " (persona_id INT);"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTablePK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTablePK + " PRIMARY KEY (persona_id);"),
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

            var transaction = connection.BeginTransaction();
            using var command = new NpgsqlCommand(connection: connection, cmdText:
                "INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES (@i, @r, @tm, @ts)");

            var id = new NpgsqlParameter<int>("i", default(int));
            var route = new NpgsqlParameter<LineString>("r", LineString.Empty);
            var transport_modes = new NpgsqlParameter<string[]>("tm", new string[1] {TransportModes.NoTransportMode});
            var time_stamps = new NpgsqlParameter<DateTime[]>("ts", new DateTime[1] {Constants.BaseDateTime});

            command.Parameters.Add(id);
            command.Parameters.Add(route);
            command.Parameters.Add(transport_modes);
            command.Parameters.Add(time_stamps);

            foreach (var persona in personas)
            {
                id.TypedValue = persona.Id;
                route.TypedValue = persona.Route;
                transport_modes.TypedValue = persona.TTextTransitions.Item1;
                time_stamps.TypedValue = persona.TTextTransitions.Item2;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();


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
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"),
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

            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + auxiliaryTable + "_comp;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            var permTableForComparison = "CREATE TABLE " + auxiliaryTable + "_comp as (SELECT * FROM " + auxiliaryTable + ");";
            await using (var cmd = new NpgsqlCommand(permTableForComparison, connection))
            {
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch(Exception e)
                {
                    logger.Debug(" ==>> Unable to save auxiliary table for comparisons {0}", e.Message);
                }                
            }
            //




            await connection.CloseAsync();

            //await PropagateResultsAsync(connectionString,auxiliaryTable,routeTable);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route uploading execution time :: {0}", totalTime);

            return uploadFails;
        }
    }
}