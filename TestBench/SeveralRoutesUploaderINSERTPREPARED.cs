using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;
using System.Text;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderINSERTPREPARED : BaseRouteUploader
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

            var _records = personas;

        var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand(connection: connection, cmdText:
            "INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES (@i, @r, @tm, @ts)");

        var id = new NpgsqlParameter<int>("i", default(int));
        var computed_route = new NpgsqlParameter<LineString>("r", LineString.Empty);
        var transport_modes = new NpgsqlParameter<string[]>("tm", new string[0]);
        var time_stamps = new NpgsqlParameter<DateTime[]>("ts", new DateTime[0]);

        command.Parameters.Add(id);
        command.Parameters.Add(computed_route);
        command.Parameters.Add(transport_modes);
        command.Parameters.Add(time_stamps);
        await command.PrepareAsync();

        foreach (var element in _records)
        {
            id.TypedValue = element.Id;
            computed_route.TypedValue = element.Route;
            transport_modes.TypedValue = element.TTextTransitions.Item1;
            time_stamps.TypedValue = element.TTextTransitions.Item2;

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