using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using SytyRouting.Model;
using System.Diagnostics;
using System.Text;

namespace SytyRouting.DataBase
{
    public class SeveralRoutesUploaderINSERTBATCHED : BaseRouteUploader
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

            using var command = new NpgsqlCommand(connection: connection, cmdText: null);

            var sb = new StringBuilder("INSERT INTO " + auxiliaryTable + " (persona_id, computed_route, transport_modes, time_stamps) VALUES ");
            for (var i = 0; i < personas.Count; i++)
            {
                if (i != 0)
                {
                    sb.Append(',');
                }
                var iName = (i * 4 + 1).ToString();
                var rName = (i * 4 + 2).ToString();
                var tmName = (i * 4 + 3).ToString();
                var tsName = (i * 4 + 4).ToString();

                sb.Append("(@").Append(iName).Append(", @").Append(rName).Append(", @").Append(tmName).Append(", @").Append(tsName).Append(')');
                command.Parameters.Add(new NpgsqlParameter<int>(iName, personas[i].Id));
                if(personas[i].Route!=null)
                {
                    command.Parameters.Add(new NpgsqlParameter<LineString?>(rName, personas[i].Route));
                }
                else
                {
                    command.Parameters.Add(new NpgsqlParameter<LineString>(rName, LineString.Empty));
                }
                command.Parameters.Add(new NpgsqlParameter<string[]>(tmName, personas[i].TTextTransitions.Item1));
                command.Parameters.Add(new NpgsqlParameter<DateTime[]>(tsName, personas[i].TTextTransitions.Item2));
            }

            command.CommandText = sb.ToString();
            await command.ExecuteNonQueryAsync();

   
            await connection.CloseAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("Route uploading execution time :: {0}", totalTime);

            return uploadFails;
        }        
    }
}