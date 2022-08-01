using NLog;
using System.Diagnostics;
using Npgsql;
using System.Globalization;
using SytyRouting.Model;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // private Persona[] PersonasArray = new Persona[0];
        private Dictionary<int, Persona> personas = new Dictionary<int, Persona>();


        public async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;
            string queryString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            

            // Get the total number of rows to estimate the creation time of the entire set of 'Personas'
            long totalDbRows = 0;
            queryString = "SELECT count(*) AS exact_count FROM public.persona";
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    totalDbRows = Convert.ToInt64(reader.GetValue(0));
                }
            }

            logger.Info("Total number of rows to process: {0}", totalDbRows);

            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                     0              1              2
            queryString = "SELECT id, home_location, work_location FROM public.persona LIMIT 100";
            
            connection.TypeMapper.UseNetTopologySuite();

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long dbRowsProcessed = 0;

                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id
                    CreatePersona(id);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % 100 == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        PersonasSetCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds);
                    }
                }
            }
        }

        public void TracePersonas()
        {
            foreach (var persona in personas)
            {
                logger.Trace("Persona Id={0}", persona.Key);
            }
        }

        private Persona CreatePersona(int id)
        {           
            var persona = new Persona { Id = id };
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