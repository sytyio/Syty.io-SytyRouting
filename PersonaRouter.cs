using NLog;
using System.Diagnostics;
using Npgsql;
using System.Globalization;
using SytyRouting.Model;
using NetTopologySuite.Geometries;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;
            string queryString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            

            // Get the total number of rows to estimate the creation time of the entire set of 'Personas'
            int totalDbRows = 0;
            queryString = "SELECT count(*) AS exact_count FROM public.persona";
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    totalDbRows = Convert.ToInt32(reader.GetValue(0));
                }
            }

            logger.Info("Total number of rows to process: {0}", totalDbRows);

            // Dictionary<int, Persona> personas = new Dictionary<int, Persona>();          // Total elapsed time: 00:02:37.178
            Dictionary<int, Persona> personas = new Dictionary<int, Persona>(totalDbRows);  // Total elapsed time: 00:02:45.795


            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                     0              1              2
            queryString = "SELECT id, home_location, work_location FROM public.persona"; //  LIMIT 10000
            
            connection.TypeMapper.UseNetTopologySuite();

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long dbRowsProcessed = 0;

                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                    var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                    CreatePersona(id, homeLocation, workLocation, personas);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % 10000 == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        PersonasSetCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds);
                    }
                }
            }
        }

        public void TracePersonas(Dictionary<int, Persona> personas)
        {
            foreach (var persona in personas)
            {
                logger.Trace("Persona: Id = {0},\n\t\t HomeLocation = ({1}, {2}),\n\t\t WorkLocation = ({3}, {4})", 
                    persona.Key, persona.Value.HomeLocation?.X, persona.Value.HomeLocation?.Y,
                    persona.Value.WorkLocation?.X, persona.Value.WorkLocation?.Y);
            }
        }

        private Persona CreatePersona(int id, Point homeLocation, Point workLocation, Dictionary<int, Persona> personas)
        {           
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            personas.Add(id, persona);

            return personas[id];
        }

        private void PersonasSetCreationBenchmark(long totalDbRows, long dbRowsProcessed, TimeSpan timeSpan, long timeSpanMilliseconds)
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var rowProcessingRate = (double)dbRowsProcessed / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var personasSetCreationTimeSeconds = totalDbRows / rowProcessingRate;
            var personasSetCreationTime = TimeSpan.FromSeconds(personasSetCreationTimeSeconds);

            var totalTime = Helper.FormatElapsedTime(personasSetCreationTime);

            logger.Debug("Number of DB rows already processed: {0}", dbRowsProcessed);
            logger.Debug("Row processing rate: {0} [Rows / s]", rowProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            logger.Info("Elapsed Time                 (HH:MM:S.mS) :: " + elapsedTime);
            logger.Info("PersonasSet creation time estimate (HH:MM:S.mS) :: " + totalTime);
        }
    }
}