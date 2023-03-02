using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class SingleRouteUploader : BaseRouteUploader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public override async Task<int> UploadRouteAsync(string connectionString, string auxiliaryTable, string routeTable, Persona persona)
        {
            Stopwatch stopWatch = new Stopwatch();

            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;

            try
            {
                var transportModes = persona.TTextTransitions.Item1;
                var timeStampsTZ = persona.TTextTransitions.Item2;

                await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES ($1, $2, $3, $4) ON CONFLICT (persona_id) DO UPDATE SET computed_route = $2, transport_modes = $3, time_stamps = $4", connection)
                {
                    Parameters =
                    {
                        new() { Value = persona.Id },
                        new() { Value = persona.Route },
                        new() { Value = transportModes },
                        new() { Value = timeStampsTZ }
                    }
                };
                await cmd_insert.ExecuteNonQueryAsync();
            }
            catch
            {
                logger.Debug(" ==>> Unable to upload route to database. Persona Id {0}", persona.Id);
                uploadFails++;
            }

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route) WHERE persona_id = " + persona.Id + ";"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route) WHERE persona_id = " + persona.Id + ";"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route) AND persona_id = " + persona.Id + ";"), 
                    //new("UPDATE " + auxiliaryTable + " SET route = computed_route::tgeompoint WHERE is_valid_route = true AND persona_id = " + persona.Id + ";")
                    new("UPDATE " + routeTable + " rt SET route = t_aux.computed_route::tgeompoint FROM " + auxiliaryTable + " t_aux WHERE  t_aux.persona_id = rt.id AND t_aux.is_valid_route = true;")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
            }

            //PLGSQL: Iterates over each transport mode transition to create the corresponding temporal text type sequence (ttext(Sequence)) for each valid route
            var iterationString = "UPDATE " + auxiliaryTable + " SET transport_sequence= coalesce_transport_modes_time_stamps(transport_modes, time_stamps) WHERE is_valid_route = true AND persona_id = " + persona.Id + ";";

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
            var updateString = "UPDATE " + routeTable + " SET route = (SELECT route FROM " + auxiliaryTable + " WHERE persona_id = " + persona.Id + ") WHERE id = " + persona.Id;

            // await using (var cmd = new NpgsqlCommand(updateString, connection))
            // {
            //     try
            //     {
            //         await cmd.ExecuteNonQueryAsync();
            //     }
            //     catch(Exception e)
            //     {
            //         logger.Debug(" ==>> Unable to update routes on the database: {0}", e.Message);
            //     }                
            // }

            updateString = "UPDATE " + routeTable + " SET transport_sequence = (SELECT transport_sequence FROM " + auxiliaryTable + " WHERE persona_id = " + persona.Id + ") WHERE id = " + persona.Id;

            await using (var cmd = new NpgsqlCommand(updateString, connection))
            {
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch(Exception e)
                {
                    logger.Debug(" ==>> Unable to update transport sequences on the database: {0}", e.Message);
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