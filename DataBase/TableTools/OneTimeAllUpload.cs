using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class OneTimeAllUpload : BaseRouteUploader
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
            
            foreach(var persona in personas)
            {
                try
                {
                    await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + auxiliaryTable + " (persona_id, computed_route) VALUES ($1, $2) ON CONFLICT (persona_id) DO UPDATE SET computed_route = $2", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = persona.Route },
                        }
                    };
                    await cmd_insert.ExecuteNonQueryAsync();
                        
                    var transportModes = persona.TTextTransitions.Item1;
                    var timeStampsTZ = persona.TTextTransitions.Item2;
                    
                    await using var cmd_insert_ttext = new NpgsqlCommand("INSERT INTO " + auxiliaryTable + " (persona_id, transport_modes, time_stamps) VALUES ($1, $2, $3) ON CONFLICT (persona_id) DO UPDATE SET transport_modes = $2, time_stamps = $3", connection)
                    {
                        Parameters =
                        {
                            new() { Value = persona.Id },
                            new() { Value = transportModes },
                            new() { Value = timeStampsTZ },
                        }
                    };
                
                    await cmd_insert_ttext.ExecuteNonQueryAsync();
                }
                catch
                {
                    logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}", persona.Id);
                    uploadFails++;
                }
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("UPDATE " + auxiliaryTable + " SET route = computed_route::tgeompoint WHERE is_valid_route = true;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

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
                    logger.Debug(" ==>> Unable to compute transport mode transitions on the database: {0}", e.Message);
                }                
            }

            //PLGSQL: Iterates over each route result to update the persona_route table
            var updateString = @"
            DO 
            $$
            DECLARE
            _id int;
            _r tgeompoint;
            _ts ttext(Sequence);
            BEGIN    
                FOR _id, _r, _ts in SELECT persona_id, route, transport_sequence FROM " + auxiliaryTable + @" ORDER BY persona_id ASC
                LOOP
                    RAISE NOTICE 'id: %', _id;
                    RAISE NOTICE 'route: %', _r;
                    RAISE NOTICE 'transport sequence: %', _ts;
                    UPDATE " + routeTable + @" SET route = _r, transport_sequence = _ts WHERE id = _id;
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
                    logger.Debug(" ==>> Unable to update routes/transport sequences on the database: {0}", e.Message);
                }                
            }
   
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route uploading execution time :: {0}", totalTime);

            return uploadFails;
        }
    }
}