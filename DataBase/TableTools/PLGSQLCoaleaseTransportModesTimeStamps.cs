using NLog;
using System.Diagnostics;
using Npgsql;

namespace SytyRouting.DataBase
{
    public static class PLGSQLFunctions
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Stopwatch stopWatch = new Stopwatch();

        public static async Task SetCoaleaseTransportModesTimeStampsFunction(string connectionString)
        {   
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // PLGSQL: Merge each transport_mode with its corresponding transition time_stamp: 
            var functionString = @"
            CREATE OR REPLACE FUNCTION coalesce_transport_modes_time_stamps(transport_modes text[], time_stamps timestamptz[]) RETURNS ttext(Sequence) AS $$
            DECLARE
            _arr_ttext ttext[];
            _seq_ttext ttext(Sequence);
            _transport_mode text;
            _index int;
            BEGIN
                _index := 0;
                FOREACH _transport_mode IN ARRAY transport_modes
                LOOP
                    _index := _index + 1;
                    RAISE NOTICE 'current tranport mode: %', _transport_mode;
                    _arr_ttext[_index] := ttext_inst(transport_modes[_index], time_stamps[_index]);            
                    RAISE NOTICE 'current ttext: %', _arr_ttext[_index];
                END LOOP;
                _seq_ttext := ttext_seq(_arr_ttext);
                RAISE NOTICE 'sequence: %', _seq_ttext;
                RETURN _seq_ttext;
                EXCEPTION
                    WHEN others THEN
                        RAISE NOTICE 'An error has occurred:';
                        RAISE NOTICE '% %', SQLERRM, SQLSTATE;
                        --RETURN ttext_seq(ARRAY[ttext_inst('None', '1970-01-01 00:00:00'), ttext_inst('None', '1970-01-01 00:00:01')]);
                        RETURN null;
            END;
            $$ LANGUAGE PLPGSQL;
            ";

            await using (var cmd = new NpgsqlCommand(functionString, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("PLGSQL function Coalease TransportModes - Time Stamps upload time :: {0}",totalTime);
        }

        public static async Task SetUnnest2D1D(string connectionString)
        {   
            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // PLGSQL: Merge each transport_mode with its corresponding transition time_stamp: 
            var functionString = @"
            CREATE OR REPLACE FUNCTION unnest_2d_1d(ANYARRAY, OUT a ANYARRAY)
                RETURNS SETOF ANYARRAY
                LANGUAGE plpgsql IMMUTABLE STRICT AS
                $func$
                    BEGIN
                        FOREACH a SLICE 1 IN ARRAY $1 LOOP
                            RETURN NEXT;
                        END LOOP;
                    END
                $func$;
            ";

            await using (var cmd = new NpgsqlCommand(functionString, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("PLGSQL function Coalease TransportModes - Time Stamps upload time :: {0}",totalTime);
        }
    }
}
