using NLog;
using System.Diagnostics;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.DataBase
{
    public class PersonaRouteAuxiliaryTable
    {
        //public string ConnectionString;
        //public string ResultTable;
        //public string AuxiliaryTable;

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Stopwatch stopWatch = new Stopwatch();
        private string _connectionString;

        public PersonaRouteAuxiliaryTable(string connectionString)
        {
            _connectionString=connectionString;
            // ResultTable=resultTable;
            // AuxiliaryTable=resultTable+Configuration.AuxiliaryTableSuffix;
        }

        public async Task<string> CreateAuxiliaryTable(string resultsTable, int numberOfRows)
        {
            stopWatch.Start();
            
            var auxiliaryTable = await SetAuxiliaryTableAsync(resultsTable, numberOfRows);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("{0} initial data set creation time :: {1}",auxiliaryTable,totalTime);

            return auxiliaryTable;
        }

        private async Task<string> SetAuxiliaryTableAsync(string resultsTable, int numberOfRows)
        {
            //var connectionString = ConnectionString;
            //var routeTable=ResultTable;
            var auxiliaryTable=resultsTable+Configuration.AuxiliaryTableSuffix;;
            var auxiliaryTableTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;
            var auxiliaryTableTableFK=auxiliaryTable+Configuration.FKConstraintSuffix;

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();
            
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            //debug: (To be removed once the table gets its definite form)
            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + auxiliaryTable + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            //

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TABLE IF NOT EXISTS " + auxiliaryTable + " AS SELECT id FROM " + resultsTable + " ORDER BY id ASC LIMIT " + numberOfRows + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " RENAME COLUMN id TO persona_id;"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTableFK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTableFK + " FOREIGN KEY (persona_id) REFERENCES " + resultsTable + " (id);"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTablePK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTablePK + " PRIMARY KEY (persona_id);"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS is_valid_route BOOL;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS route TGEOMPOINT;"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_modes TEXT[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS time_stamps TIMESTAMPTZ[];"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table creation",auxiliaryTable);
            }

            await connection.CloseAsync();

            return auxiliaryTable;
        }
    }
}
