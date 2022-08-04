using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private IEnumerable<Persona> Personas = new List<Persona>(0); // For debugging
        private IEnumerable<Persona> SortedPersonas = new List<Persona>(0); // For debugging

        private static int numberOfQueues = 7;
        private static int numberOfBatchesPerQueue = 3;
        private int numberOfBatches = numberOfQueues * numberOfBatchesPerQueue;

                                                                                                // Total elapsed time:
        // Dictionary<int, Persona> personas = new Dictionary<int, Persona>();                  // 00:02:37.178
        // Dictionary<int, Persona> personas = new Dictionary<int, Persona>(totalDbRows);       // 00:02:45.795
        // Queue<Persona> personas = new Queue<Persona>(totalDbRows);                           // 00:02:55.881
        // Queue<Persona>[] personaQueues = new Queue<Persona>[numberOfQueues];                 // 00:02:36.108 (no individual size initialization),
                                                                                                // 00:03:01.740 (individual size initialization),
                                                                                                // 00:04:13.284 (individual size initialization, sequential queue switching)
        private ConcurrentQueue<Persona>[] personaQueues = new ConcurrentQueue<Persona>[numberOfQueues];// 00:04:13.539 (using ConcurrentQueues, sequential queue switching)

        public PersonaRouter()
        {
            // Initialize personaQueues
            for (var i = 0; i < personaQueues.Length; i++)
            {
                personaQueues[i] = (personaQueues[i]) ?? new ConcurrentQueue<Persona>();
            }
        }

        public async Task StartRouting()
        {
            await Task.Run(() => DBPersonaLoadAsync()); // Not really sure at this point if a Parallel.Invoke(.) should be used instead...
        }

        private async Task DBPersonaLoadAsync()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;

            var tableName = "public.persona";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            // var totalDbRows = await Helper.DbTableRowsCount(connection, tableName, logger);
            var totalDbRows = 100;

            
            var regularBatchSize = totalDbRows / numberOfBatches;
            var lastBatchSize = regularBatchSize + totalDbRows % numberOfBatches;


            int[] batchSizes = new int[numberOfBatches];
            for (var i = 0; i < batchSizes.Length-1; i++)
            {
                batchSizes[i] = regularBatchSize;
            }
            batchSizes[batchSizes.Length-1] = lastBatchSize;


            int dbRowsProcessed = 0;
            var currentQueue = 0;
            var offset = 0;

            for(var b = 0; b < numberOfBatches; b++)
            {
                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                     0              1              2
                var queryString = "SELECT id, home_location, work_location FROM " + tableName + " ORDER BY id ASC LIMIT " + batchSizes[b] + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)

                        CreatePersona(id, homeLocation, workLocation, currentQueue);

                        dbRowsProcessed++;

                        if (dbRowsProcessed % 50_000 == 0)
                        {
                            logger.Debug("Queue #{0}: {1} elements (batch #{2})", currentQueue, personaQueues[currentQueue].Count, b);
                            var timeSpan = stopWatch.Elapsed;
                            var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                            Helper.SetCreationBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                        }
                    }
                }
                offset = offset + batchSizes[b];
                currentQueue = CycleQueue(currentQueue);
            }

            // For debugging
            int numberOfQueueElements = 0;
            foreach(var queue in personaQueues)
            {
                numberOfQueueElements = numberOfQueueElements + queue.Count;
                Personas = Personas.Concat(queue.ToList());
            }
            SortedPersonas = Personas.OrderBy(p => p.Id);

                

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("                           Persona set creation time :: " + totalTime);
            logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
            logger.Debug("Total number of elements in queues: {0}", numberOfQueueElements);
        }

        public void TracePersonas()
        {
            foreach (var persona in Personas)
            {
                logger.Trace("Persona: Id = {0},\n\t\t HomeLocation = ({1}, {2}),\n\t\t WorkLocation = ({3}, {4})", 
                    persona.Id, persona.HomeLocation?.X, persona.HomeLocation?.Y,
                    persona.WorkLocation?.X, persona.WorkLocation?.Y);
            }
        }

        public void TracePersonaIds()
        {
            logger.Debug("Personas Ids:");
            foreach (var persona in Personas)
            {
                logger.Trace("Persona: Id = {0}", persona.Id);
            }

            logger.Debug("SortedPersonas Ids:");
            foreach (var persona in SortedPersonas)
            {
                logger.Trace("Persona: Id = {0}", persona.Id);
            }
        }

        private Persona CreatePersona(int id, Point homeLocation, Point workLocation, int currentQueue)
        {           
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            personaQueues[currentQueue].Enqueue(persona);

            return persona;
        }

        private int CycleQueue(int currentQueue)
        {
            currentQueue++;
            if(currentQueue >= numberOfQueues)
                currentQueue = 0;

            return currentQueue;
        }
    }
}