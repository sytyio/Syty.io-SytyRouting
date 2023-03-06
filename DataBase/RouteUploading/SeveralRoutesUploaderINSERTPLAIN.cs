using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;
using System.Text;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderINSERTPLAIN : BaseRouteUploader
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

            //var _records = personas;


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

   
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route uploading execution time :: {0}", totalTime);

            return uploadFails;
        }

        //public static async Task<int> PropagateResultsStaticAsync(string connectionString, string auxiliaryTable, string routeTable)
        //public override async Task<int> PropagateResultsAsync(string connectionString, string auxiliaryTable, string routeTable)
        public static async Task<int> PropagateResultsAsync(string connectionString, string auxiliaryTable, string routeTable)
        {
            Stopwatch stopWatch = new Stopwatch();

            stopWatch.Start();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite(new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM));

            int uploadFails = 0;

            await using var batch = new NpgsqlBatch(connection)
            {
                BatchCommands =
                {
                    new("UPDATE " + auxiliaryTable + " SET computed_route_2d = st_force2d(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = st_IsValidTrajectory(computed_route);"),
                    new("UPDATE " + auxiliaryTable + " SET is_valid_route = false WHERE st_IsEmpty(computed_route);"),
                    //new("UPDATE " + auxiliaryTable + " SET route = computed_route::tgeompoint WHERE is_valid_route = true;")
                    new("UPDATE " + routeTable + " rt SET route = t_aux.computed_route::tgeompoint FROM " + auxiliaryTable + " t_aux WHERE  t_aux.persona_id = rt.id AND t_aux.is_valid_route = true;")
                }
            };

            await using (var reader = await batch.ExecuteReaderAsync())
            {
                logger.Debug("{0} table SET statements executed",auxiliaryTable);
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
                    uploadFails++;
                }                
            }

            //PLGSQL: Iterates over each route result to update the persona_route table
            // var updateString = @"
            // DO 
            // $$
            // DECLARE
            // _id int;
            // _r tgeompoint;
            // _ts ttext(Sequence);
            // BEGIN    
            //     FOR _id, _r, _ts in SELECT persona_id, route, transport_sequence FROM " + auxiliaryTable + @" ORDER BY persona_id ASC
            //     LOOP
            //         RAISE NOTICE 'id: %', _id;
            //         RAISE NOTICE 'route: %', _r;
            //         RAISE NOTICE 'transport sequence: %', _ts;
            //         UPDATE " + routeTable + @" SET route = _r, transport_sequence = _ts WHERE id = _id;
            //     END LOOP;
            // END;
            // $$;
            // ";

            // await using (var cmd = new NpgsqlCommand(updateString, connection))
            // {
            //     try
            //     {
            //         await cmd.ExecuteNonQueryAsync();
            //     }
            //     catch(Exception e)
            //     {
            //         logger.Debug(" ==>> Unable to update routes/transport sequences on the database: {0}", e.Message);
            //         uploadFails++;
            //     }                
            // }

            return uploadFails;
        }
    }
}