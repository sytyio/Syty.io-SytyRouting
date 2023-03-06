using NLog;
using System.Diagnostics;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.DataBase
{
    public class PersonaRouteTable
    {
        //public string PersonaOriginTable;
        //public string RoutingResultTable;
        //public string AuxiliaryTable = "";

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Stopwatch stopWatch = new Stopwatch();
        //private int _numberOfRows = 0;
        private string _connectionString;

        public PersonaRouteTable(string connectionString)
        {
            _connectionString=connectionString;
        }

        public async Task<string> CreateDataSet(string personaOriginTable, string routingResultTable, int numberOfRows)
        {
            stopWatch.Start();

            //PersonaOriginTable=personaOriginTable;
            //_numberOfRows=numberOfRows;
            
            await SetRoutingResultTableAsync(personaOriginTable,routingResultTable,numberOfRows);

            var personaRouteAuxTable = new PersonaRouteAuxiliaryTable(_connectionString);
            var auxiliaryTable = await personaRouteAuxTable.CreateAuxiliaryTable(routingResultTable,numberOfRows);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("{0} initial data set creation time :: {1}", routingResultTable,totalTime);

            return auxiliaryTable;
        }

        public async Task<string> CreateDataSetEmptyAuxTab(string personaOriginTable, string routingResultTable, int numberOfRows)
        {
            stopWatch.Start();

            //PersonaOriginTable=personaOriginTable;
            //_numberOfRows=numberOfRows;
            
            await SetRoutingResultTableAsync(personaOriginTable,routingResultTable,numberOfRows);

            var personaRouteAuxTable = new PersonaRouteAuxiliaryTable(_connectionString);
            var auxiliaryTable = await personaRouteAuxTable.CreateEmptyAuxiliaryTable(routingResultTable,numberOfRows);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("{0} initial data set creation time :: {1}", routingResultTable,totalTime);

            return auxiliaryTable;
        }

        private async Task SetRoutingResultTableAsync(string personaOriginTable, string routingResultTable, int numberOfRows)
        {
            var routingResultTablePK=routingResultTable+Configuration.PKConstraintSuffix;

            List<int> personaIds = new List<int>(numberOfRows);

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();            
            
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            //debug: (To be removed once the table gets its definite form)
            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + routingResultTable + " CASCADE;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            //

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    //debug:
                    new("CREATE TABLE IF NOT EXISTS " + routingResultTable + " AS SELECT id, home_location, work_location FROM " + personaOriginTable + " WHERE id = 12068 ORDER BY id ASC LIMIT " + numberOfRows + ";"),
                    //
                    //new("CREATE TABLE IF NOT EXISTS " + routingResultTable + " AS SELECT id, home_location, work_location FROM " + personaOriginTable + " ORDER BY id ASC LIMIT " + numberOfRows + ";"),
                    new("ALTER TABLE " + routingResultTable + " DROP CONSTRAINT IF EXISTS " + routingResultTablePK + ";"),
                    new("ALTER TABLE " + routingResultTable + " ADD CONSTRAINT " + routingResultTablePK + " PRIMARY KEY (id);"),
                    new("ALTER TABLE " + routingResultTable + " ADD COLUMN IF NOT EXISTS requested_transport_modes TEXT[];"),
                    new("ALTER TABLE " + routingResultTable + " ADD COLUMN IF NOT EXISTS start_time TIMESTAMPTZ;"),
                    new("ALTER TABLE " + routingResultTable + " ADD COLUMN IF NOT EXISTS route TGEOMPOINT;"),
                    new("ALTER TABLE " + routingResultTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("++++++++++++++++++++++++++++++++++++++++++");
                logger.Debug("   {0} table creation   ",routingResultTable);
                logger.Debug("++++++++++++++++++++++++++++++++++++++++++");
            }

            // Insert default transport sequence.
            var queryString = "SELECT id FROM " + routingResultTable + " ORDER BY id ASC;";

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
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + routingResultTable + " (id, requested_transport_modes, start_time) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET requested_transport_modes = $2, start_time = $3", connection)
                    {
                        Parameters =
                        {
                            new() { Value = personaId },
                            new() { Value = Configuration.DefaultTransportSequence },
                            new() { Value = Configuration.DefaultRouteStartDateTime }
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
