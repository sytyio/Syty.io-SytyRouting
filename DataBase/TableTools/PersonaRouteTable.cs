using NLog;
using System.Diagnostics;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.DataBase
{
    public class PersonaRouteTable
    {
        public string ConnectionString;
        public string PersonaOriginTable;
        public string RoutingResultTable;
        public string AuxiliaryTable = "";

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Stopwatch stopWatch = new Stopwatch();

        public PersonaRouteTable(string personaOriginTable, string personaRouteTable, string connectionString)
        {
            ConnectionString=connectionString;
            PersonaOriginTable=personaOriginTable;
            RoutingResultTable=personaRouteTable;
        }

        public async Task CreateDataSet()
        {
            stopWatch.Start();
            
            await SetRoutingResultTableAsync();

            var personaRouteAuxTable = new PersonaRouteAuxiliaryTable(RoutingResultTable,ConnectionString);
            await personaRouteAuxTable.CreateAuxiliaryTable();
            AuxiliaryTable = personaRouteAuxTable.AuxiliaryTable;

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("{0} initial data set creation time :: {1}", RoutingResultTable,totalTime);
        }

        private async Task SetRoutingResultTableAsync()
        {
            var connectionString = ConnectionString;
            var personaOriginTable=PersonaOriginTable;
            var routeResultTable=RoutingResultTable;

            List<int> personaIds = new List<int>(0);

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();            
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + routeResultTable + " CASCADE;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS " + routeResultTable + " AS SELECT id, home_location, work_location FROM " + personaOriginTable + " ORDER BY id ASC LIMIT 10;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " DROP CONSTRAINT IF EXISTS persona_route_pk;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " ADD CONSTRAINT persona_route_pk PRIMARY KEY (id);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " ADD COLUMN IF NOT EXISTS requested_transport_modes TEXT[];", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " ADD COLUMN IF NOT EXISTS start_time TIMESTAMPTZ;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " ADD COLUMN IF NOT EXISTS route TGEOMPOINT;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("ALTER TABLE " + routeResultTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Insert default transport sequence.
            var queryString = "SELECT id FROM " + routeResultTable + " ORDER BY id ASC;";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    var persona_id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    personaIds.Add(persona_id);
                }
            }

            foreach(var personaId in personaIds)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routeResultTable + " (id, requested_transport_modes, start_time) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET requested_transport_modes = $2, start_time = $3", connection)
                    {
                        Parameters =
                        {
                            new() { Value = personaId },
                            new() { Value = Configuration.DefaultBenchmarkSequence },
                            new() { Value = Configuration.DefaultRouteStartTime }
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Debug(" ==>> Unable to upload record to database. Persona Id {0}", personaId);
                }
            }
            
            await connection.CloseAsync();
        }
    }
}
