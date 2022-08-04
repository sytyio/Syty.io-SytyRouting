using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Persona[] PersonasArray = new Persona[0];

        private static int numberOfQueues = 1;
        private static int numberOfBatchesPerQueue = 1;
        private int numberOfBatches = numberOfQueues * numberOfBatchesPerQueue;

        public async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;

            var tableName = "public.persona";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            var totalDbRows = await Helper.DbTableRowsCount(connection, tableName, logger);
            // var totalDbRows = 100;

            // Dictionary<int, Persona> personas = new Dictionary<int, Persona>();             // Total elapsed time: 00:02:37.178
            // Dictionary<int, Persona> personas = new Dictionary<int, Persona>(totalDbRows);  // Total elapsed time: 00:02:45.795
            // Queue<Persona> personas = new Queue<Persona>(totalDbRows);                      // Total elapsed time: 00:02:55.881
            Queue<Persona>[] personaQueues = new Queue<Persona>[numberOfQueues];               // Total elapsed time: 00:02:36.108 (no individual size initialization)
            for (var i = 0; i < personaQueues.Length-1; i++)
            {
                personaQueues[i] = (personaQueues[i]) ?? new Queue<Persona>();
            }
            personaQueues[personaQueues.Length-1] = (personaQueues[personaQueues.Length-1]) ?? new Queue<Persona>();

            


            // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
            //                     0              1              2
            var queryString = "SELECT id, home_location, work_location FROM " + tableName + " ORDER BY id ASC"; // + " LIMIT 100";

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long dbRowsProcessed = 0;

                while(await reader.ReadAsync())
                {
                    var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                    var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                    var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                    CreatePersona(id, homeLocation, workLocation, personaQueues[0]);

                    dbRowsProcessed++;

                    if (dbRowsProcessed % 50_000 == 0)
                    {                        
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        Helper.SetCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                    }
                }

                // PersonasArray = personas.ToArray();

                stopWatch.Stop();
                var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
                logger.Info("                           Persona set creation time :: " + totalTime);
                logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
            }
        }

        public void TracePersonas()
        {
            foreach (var persona in PersonasArray)
            {
                logger.Trace("Persona: Id = {0},\n\t\t HomeLocation = ({1}, {2}),\n\t\t WorkLocation = ({3}, {4})", 
                    persona.Id, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                    persona.WorkLocation?.X, persona.WorkLocation?.Y);
            }
        }

        private Persona CreatePersona(int id, Point homeLocation, Point workLocation, Queue<Persona> personas)
        {           
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            personas.Enqueue(persona);

            return persona; // Queue
        }
    }
}