using NLog;
using System.Diagnostics;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.DataBase
{
    public class PersonaRouteAuxiliaryTable
    {
        public string ConnectionString;
        public string ResultTable;
        public string AuxiliaryTable;

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Stopwatch stopWatch = new Stopwatch();

        public PersonaRouteAuxiliaryTable(string resultTable, string connectionString)
        {
            ConnectionString=connectionString;
            ResultTable=resultTable;
            AuxiliaryTable=resultTable+Configuration.AuxiliaryTableSuffix;
        }

        public async Task CreateAuxiliaryTable()
        {
            stopWatch.Start();
            
            await SetAuxiliaryTableAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("{0} initial data set creation time :: {1}", AuxiliaryTable,totalTime);
        }

        private async Task SetAuxiliaryTableAsync()
        {
            var connectionString = ConnectionString;
            var routeTable=ResultTable;
            var auxiliaryTable=AuxiliaryTable;
            var auxiliaryTableTablePK=auxiliaryTable+Configuration.PKConstraintSuffix;
            var auxiliaryTableTableFK = Configuration.FKConstraintSuffix;

            // Create a factory using default values (e.g. floating precision)
			GeometryFactory geometryFactory = new GeometryFactory();
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            //debug: (To be removed once the table gets its definite form)
            await using (var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS " + auxiliaryTable + ";", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            //

            // await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS " + auxiliaryTable + " AS SELECT id FROM " + routeTable + " ORDER BY id ASC LIMIT 10;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " RENAME COLUMN id TO persona_id;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTableFK + ";", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTableFK + " FOREIGN KEY (persona_id) REFERENCES " + routeTable + " (id);", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTablePK + ";", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTablePK + " PRIMARY KEY (persona_id);", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route GEOMETRY;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS computed_route_2d GEOMETRY;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS is_valid_route BOOL;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS route TGEOMPOINT;", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_modes TEXT[];", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS time_stamps TIMESTAMPTZ[];", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            // await using (var cmd = new NpgsqlCommand("ALTER TABLE " + auxiliaryTable + " ADD COLUMN IF NOT EXISTS transport_sequence TTEXT(Sequence);", connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("CREATE TABLE IF NOT EXISTS " + auxiliaryTable + " AS SELECT id FROM " + routeTable + " ORDER BY id ASC LIMIT 10;"),
                    new("ALTER TABLE " + auxiliaryTable + " RENAME COLUMN id TO persona_id;"),
                    new("ALTER TABLE " + auxiliaryTable + " DROP CONSTRAINT IF EXISTS " + auxiliaryTableTableFK + ";"),
                    new("ALTER TABLE " + auxiliaryTable + " ADD CONSTRAINT " + auxiliaryTableTableFK + " FOREIGN KEY (persona_id) REFERENCES " + routeTable + " (id);"),
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

            // // PLGSQL: Merge each transport_mode with its corresponding transition time_stamp: 
            // var functionString = @"
            // CREATE OR REPLACE FUNCTION coalesce_transport_modes_time_stamps(transport_modes text[], time_stamps timestamptz[]) RETURNS ttext(Sequence) AS $$
            // DECLARE
            // _arr_ttext ttext[];
            // _seq_ttext ttext(Sequence);
            // _transport_mode text;
            // _index int;
            // BEGIN
            //     _index := 0;
            //     FOREACH _transport_mode IN ARRAY transport_modes
            //     LOOP
            //         _index := _index + 1;
            //         RAISE NOTICE 'current tranport mode: %', _transport_mode;
            //         _arr_ttext[_index] := ttext_inst(transport_modes[_index], time_stamps[_index]);            
            //         RAISE NOTICE 'current ttext: %', _arr_ttext[_index];
            //     END LOOP;
            //     _seq_ttext := ttext_seq(_arr_ttext);
            //     RAISE NOTICE 'sequence: %', _seq_ttext;
            //     RETURN _seq_ttext;
            //     EXCEPTION
            //         WHEN others THEN
            //             RAISE NOTICE 'An error has occurred:';
            //             RAISE NOTICE '% %', SQLERRM, SQLSTATE;
            //             --RETURN ttext_seq(ARRAY[ttext_inst('None', '1970-01-01 00:00:00'), ttext_inst('None', '1970-01-01 00:00:01')]);
            //             RETURN null;
            // END;
            // $$ LANGUAGE PLPGSQL;
            // ";

            // await using (var cmd = new NpgsqlCommand(functionString, connection))
            // {
            //     await cmd.ExecuteNonQueryAsync();
            // }

            await connection.CloseAsync();
        }
    }
}
