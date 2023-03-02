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
            
            // foreach(var persona in personas)
            // {
            //     try
            //     {
            //         var transportModes = persona.TTextTransitions.Item1;
            //         var timeStampsTZ = persona.TTextTransitions.Item2;

            //         await using var cmd_insert = new NpgsqlCommand("INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES ($1, $2, $3, $4) ON CONFLICT (persona_id) DO UPDATE SET computed_route = $2, transport_modes = $3, time_stamps = $4", connection)
            //         {
            //             Parameters =
            //             {
            //                 new() { Value = persona.Id },
            //                 new() { Value = persona.Route },
            //                 new() { Value = transportModes },
            //                 new() { Value = timeStampsTZ }
            //             }
            //         };
            //         await cmd_insert.ExecuteNonQueryAsync();
            //     }
            //     catch
            //     {
            //         logger.Debug(" ==>> Unable to upload route data to database. Persona Id {0}", persona.Id);
            //         uploadFails++;
            //     }
            // }

            // using var importer = connection.BeginBinaryImport("COPY " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) FROM STDIN (FORMAT binary)");

            var _records = personas;
            // foreach (var element in _records)
            // {
            //     await importer.StartRowAsync();
            //     await importer.WriteAsync(element.Id);
            //     await importer.WriteAsync(element.Route);
            //     await importer.WriteAsync(element.TTextTransitions.Item1);
            //     await importer.WriteAsync(element.TTextTransitions.Item2);
            // }

            // await importer.CompleteAsync();




            // using var command = new NpgsqlCommand(connection: connection, cmdText: null);

            // var sb = new StringBuilder("INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES ");
            // for (var i = 0; i < _records.ToArray().Length; i++)
            // {
            //     if (i != 0) sb.Append(',');
            //     var iName = (i * 4 + 1).ToString();
            //     var rName = (i * 4 + 2).ToString();
            //     var tmName = (i * 4 + 3).ToString();
            //     var tsName = (i * 4 + 4).ToString();

            //     sb.Append("(@").Append(iName).Append(", @").Append(rName).Append(", @").Append(tmName).Append(", @").Append(tsName).Append(')');
            //     command.Parameters.Add(new NpgsqlParameter<int>(iName, _records[i].Id));
            //     command.Parameters.Add(new NpgsqlParameter<LineString>(rName, _records[i].Route));
            //     command.Parameters.Add(new NpgsqlParameter<string[]>(tmName, _records[i].TTextTransitions.Item1));
            //     command.Parameters.Add(new NpgsqlParameter<DateTime[]>(tsName, _records[i].TTextTransitions.Item2));
            // }

            // command.CommandText = sb.ToString();
            // await command.ExecuteNonQueryAsync();



        var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand(connection: connection, cmdText:
            "INSERT INTO " + auxiliaryTable + " (persona_id, computed_route) VALUES (@i, @n)");

        var id = new NpgsqlParameter<int>("i", default(int));
        var route = new NpgsqlParameter<LineString>("n", LineString.Empty);

        command.Parameters.Add(id);
        command.Parameters.Add(route);

        foreach (var element in _records)
        {
            id.TypedValue = element.Id;
            route.TypedValue = element.Route;

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
                    new("UPDATE " + auxiliaryTable + " SET route = computed_route::tgeompoint WHERE is_valid_route = true;")
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
                    uploadFails++;
                }                
            }

            return uploadFails;
        }
    }
}