using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderUNNEST : BaseRouteUploader
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

            // // using var importer = connection.BeginBinaryImport("COPY " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) FROM STDIN (FORMAT binary)");

            var _records = personas;
            // // foreach (var element in _records)
            // // {
            // //     await importer.StartRowAsync();
            // //     await importer.WriteAsync(element.Id);
            // //     await importer.WriteAsync(element.Route);
            // //     await importer.WriteAsync(element.TTextTransitions.Item1);
            // //     await importer.WriteAsync(element.TTextTransitions.Item2);
            // // }

            // // await importer.CompleteAsync();

          
          
            //using var command = new NpgsqlCommand(connection: connection, cmdText: "INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) SELECT * FROM unnest(@i, @r, @tm, @ts) AS d");
            //using var command = new NpgsqlCommand(connection: connection, cmdText: "INSERT INTO " + auxiliaryTable + " (persona_id) SELECT * FROM unnest(@i) AS d ON CONFLICT (persona_id) DO UPDATE SET is_valid_route = true;");
            //using var command = new NpgsqlCommand(connection: connection, cmdText: "INSERT INTO " + auxiliaryTable + " (persona_id, transport_modes) SELECT * FROM unnest(@i,@tm) AS d ON CONFLICT (persona_id) DO UPDATE SET transport_modes = @tm;");
            using var command = new NpgsqlCommand(connection: connection, cmdText: "INSERT INTO " + auxiliaryTable + " (persona_id, time_stamps) SELECT * FROM unnest(@i) as _id SELECT * FROM unnest_2d_1d(@ts) AS _ts ON CONFLICT (persona_id) DO UPDATE SET time_stamps = @ts;");

            command.Parameters.Add(new NpgsqlParameter<int[]>("i", _records.Select(e => e.Id).ToArray()));
            //command.Parameters.Add(new NpgsqlParameter<LineString[]>("r", _records.Select(e => e.Route).ToArray()));
            var personasArray = personas.ToArray();
            var transportModes = new List<string[]>(personasArray.Length);
            for(int i=0; i<personasArray.Length; i++)
            {
                transportModes.Add(personasArray[i].TTextTransitions.Item1);
            }
            var transportModesArray = transportModes.ToArray();

            var eitme1s=_records.Select(e => e.TTextTransitions.Item1).ToArray();
            
            //command.Parameters.Add(new NpgsqlParameter<string[][]>("tm", transportModesArray));

            //command.Parameters.Add(new NpgsqlParameter<List<string[]>>("tm", transportModes));
            //command.Parameters.Add(new NpgsqlParameter<string[][]>("tm", _records.Select(e => e.TTextTransitions.Item1).ToArray()));



            command.Parameters.Add(new NpgsqlParameter<DateTime[][]>("ts", _records.Select(e => e.TTextTransitions.Item2).ToArray()));


            //command.Parameters.Add(new NpgsqlParameter<string[][]>("tm", _records.Select(e => e.TTextTransitions.Item1).ToArray()));

            await command.ExecuteNonQueryAsync();


   
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route uploading execution time :: {0}", totalTime);

            return uploadFails;
        }

        
    }
}