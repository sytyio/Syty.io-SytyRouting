using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;

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
    }
}