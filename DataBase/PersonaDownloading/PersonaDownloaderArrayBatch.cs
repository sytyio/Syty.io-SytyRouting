using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;

namespace SytyRouting.DataBase
{
    public class PersonaDownloaderArrayBatch : BasePersonaDownloader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();       

        public override async Task<Persona[]> DownloadPersonasAsync(string connectionString, string personaTable, int batchSize, int offset)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();


            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var personaTaskArray = new Persona[batchSize];
            var personaIndex = 0;

            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                        0   1              2              3           4
            var queryString = "SELECT id, home_location, work_location, start_time, requested_transport_modes FROM " + personaTable + " ORDER BY id ASC LIMIT " + batchSize + " OFFSET " + offset;

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                    var workLocation = (Point)reader.GetValue(2); // work_location (Point)
                    var startTime = (DateTime)reader.GetValue(3); // start_time (TIMESTAMPTZ)
                    var requestedSequence = reader.GetValue(4); // transport_sequence (text[])
                    byte[] requestedTransportSequence;
                    if(requestedSequence is not null && requestedSequence != DBNull.Value)
                    {
                            requestedTransportSequence = ValidateTransportSequence(id, homeLocation, workLocation, (string[])requestedSequence);
                    }
                    else
                    {
                        requestedTransportSequence = new byte[0];
                    }

                    var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation, StartDateTime = startTime, RequestedTransportSequence = requestedTransportSequence};
                    
                    personaTaskArray[personaIndex] = persona;
                    personaIndex++;
                }
            }
            
            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Debug("ddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");
            logger.Debug("   Personas downloading execution time :: {0}", totalTime);
            logger.Debug("ddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");

            return personaTaskArray;
        }
    }
}
