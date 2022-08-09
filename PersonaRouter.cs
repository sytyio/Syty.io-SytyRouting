using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;
using SytyRouting.Algorithms;
using System.Globalization;

namespace SytyRouting
{
    public class PersonaRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private IEnumerable<Persona> Personas = new List<Persona>(0); // For debugging
        private IEnumerable<Persona> SortedPersonas = new List<Persona>(0); // For debugging
        private IEnumerable<Persona> PersonaIdsForCalculatedRoutes = new List<Persona>(0); // For debugging
        private IEnumerable<Persona> SortedPersonaIdsForCalculatedRoutes = new List<Persona>(0); // For debugging

        private Graph _graph;

        private static int numberOfQueues = Environment.ProcessorCount; // 8-core computer;
        // private static int numberOfQueues = 3; // 8-core computer;
        private static int numberOfBatchesPerQueue = 1;
        private int numberOfBatches = numberOfQueues * numberOfBatchesPerQueue;
        
        private int numberOfExpectedRoutes;

        private ConcurrentDictionary<int, QueueBenchmark> queueBenchmarks = new ConcurrentDictionary<int, QueueBenchmark>(numberOfQueues, numberOfQueues); // For debugging
                                                                                                // Total elapsed time:
        // Dictionary<int, Persona> personas = new Dictionary<int, Persona>();                  // 00:02:37.178
        // Dictionary<int, Persona> personas = new Dictionary<int, Persona>(totalDbRows);       // 00:02:45.795
        // Queue<Persona> personas = new Queue<Persona>(totalDbRows);                           // 00:02:55.881
        // Queue<Persona>[] personaQueues = new Queue<Persona>[numberOfQueues];                 // 00:02:36.108 (no individual size initialization),
                                                                                                // 00:03:01.740 (individual size initialization),
                                                                                                // 00:04:13.284 (individual size initialization, sequential queue switching)
        private ConcurrentQueue<Persona>[] PersonaQueues = new ConcurrentQueue<Persona>[numberOfQueues];// 00:04:13.539 (using ConcurrentQueues, sequential queue switching)

        ConcurrentBag<Persona> personaIdsForCalculatedRoutes = new ConcurrentBag<Persona>(); // For degugging


        public PersonaRouter(Graph graph)
        {
            _graph = graph;

            for (var i = 0; i < PersonaQueues.Length; i++)
            {
                PersonaQueues[i] = (PersonaQueues[i]) ?? new ConcurrentQueue<Persona>();
            }

            for (var i = 0; i < numberOfQueues; i++)
            {
                if(!queueBenchmarks.TryAdd(i, new QueueBenchmark {Id = i}))
                    logger.Debug("Failed to initialize queues benchmarks at queue #{0}", i);
            }
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            logger.Debug("> StartRouting initiated");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var routingAlgorithms = new IRoutingAlgorithm[numberOfQueues];
            for(var a = 0; a < routingAlgorithms.Length; a++)
            {
                routingAlgorithms[a] = new T();
                routingAlgorithms[a].Initialize(_graph);
            }
            logger.Info("Route searching using {0}'s algorithm on {1} parallel routing queue(s)", routingAlgorithms[0].GetType().Name, numberOfQueues);
            

            var connectionString = Constants.ConnectionString;
            var tableName = "public.persona";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            // numberOfExpectedRoutes = await Helper.DbTableRowCount(connection, tableName, logger);
            numberOfExpectedRoutes = 1000;

            
            
            
            
            // ////////////////////////// //
            // async-awaited (sequential) //
            // ////////////////////////// //
            // await Task.Run(() => DBPersonaLoadAsync(connection, tableName, numberOfExpectedRoutes)); // Not really sure at this point if a Parallel.Invoke(.) should be used instead...
            // for(var q = 0; q < numberOfQueues; q++)
            // {
            //     await Task.Run(() => CalculateRoutesForQueue(routingAlgorithms[q], q));
            // }




            // ////////////////////////////////////////////////////////// //
            // awaited Queues load (sequential), queue routing (parallel) //
            // ////////////////////////////////////////////////////////// //
            await Task.Run(() => DBPersonaLoadAsync(connection, tableName, numberOfExpectedRoutes)); // Not really sure at this point if a Parallel.Invoke(.) should be used instead...
            try
            {
                Parallel.For(0, numberOfQueues, (q) => CalculateRoutesForQueue(routingAlgorithms[q], q));
            }
            catch (AggregateException e)
            {
                logger.Debug("Parallel.For has thrown the following (unexpected) exception:\n{0}", e);
            }

            



            // Action version
            //Action action1 = () => DBPersonaLoadAsync(connection, tableName, numberOfExpectedRoutes);


            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time using {0} algorithm :: {1}",routingAlgorithms[0].GetType().Name, totalTime);

            logger.Debug("> StartRouting ended");
        }

        private async Task DBPersonaLoadAsync(NpgsqlConnection connection, string tableName, int totalDbRows)
        {
            logger.Debug("> DBPersonaLoadAsync started");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
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
                            logger.Debug("Queue #{0}: {1} elements (batch #{2})", currentQueue, PersonaQueues[currentQueue].Count, b);
                            var timeSpan = stopWatch.Elapsed;
                            var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                            Helper.DataLoadBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                        }
                    }
                }
                offset = offset + batchSizes[b];
                currentQueue = ChangeQueue(currentQueue);
            }

            // For debugging
            int numberOfQueueElements = 0;
            foreach(var queue in PersonaQueues)
            {
                numberOfQueueElements = numberOfQueueElements + queue.Count;
                Personas = Personas.Concat(queue.ToList());
            }
            SortedPersonas = Personas.OrderBy(p => p.Id);

                
            
            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("                           Persona set creation time :: " + totalTime);
            logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
            logger.Debug("Number of Queues: {0}", numberOfQueues);
            logger.Debug("Total number of elements in queues: {0}", numberOfQueueElements);

            logger.Debug("> DBPersonaLoadAsync ended");
        }

        private Persona CreatePersona(int id, Point homeLocation, Point workLocation, int currentQueue)
        {   
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            PersonaQueues[currentQueue].Enqueue(persona);

            return persona;
        }

        private void CalculateRoutesForQueue(IRoutingAlgorithm routingAlgorithm, int queueNumber)
        {   
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            int calculatedRoutes = 0;

            logger.Debug("> Thread #{0}\t=>\tQueue #{1}", Thread.CurrentThread.ManagedThreadId, queueNumber);
            logger.Debug("> CalculateRoutesForQueue started for Queue #{0}", queueNumber);
            while(PersonaQueues[queueNumber].TryDequeue(out Persona? persona))
            {
                var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);
        
                personaIdsForCalculatedRoutes.Add(persona);
                calculatedRoutes++;

                if (calculatedRoutes % 100 == 0)
                {
                    var timeSpan = stopWatch.Elapsed;
                    var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                    QueueRoutingBenchmark(numberOfExpectedRoutes  / numberOfQueues, calculatedRoutes, timeSpan, timeSpanMilliseconds, queueNumber);
                }
            }           
            logger.Debug("> CalculateRoutesForQueue ended for Queue #{0}", queueNumber);
        }

        private int ChangeQueue(int currentQueue)
        {
            return (++currentQueue >= numberOfQueues) ? 0 : currentQueue;
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
                logger.Debug("Persona: Id = {0}", persona.Id);
            }

            logger.Debug("SortedPersonas Ids:");
            foreach (var persona in SortedPersonas)
            {
                logger.Debug("Persona: Id = {0}", persona.Id);
            }
        }

        public void TracePersonaIdsForCalculatedRoutes()
        {
            PersonaIdsForCalculatedRoutes = personaIdsForCalculatedRoutes.ToArray();
            SortedPersonaIdsForCalculatedRoutes = PersonaIdsForCalculatedRoutes.OrderBy(p => p.Id);

            var sortedPersonaIds = SortedPersonas.ToList();
            var sortedPersonaIdsForCalculatedRoutes = SortedPersonaIdsForCalculatedRoutes.ToList();

            var comparisonResult = Enumerable.SequenceEqual(sortedPersonaIdsForCalculatedRoutes, sortedPersonaIds);

            if(comparisonResult)
            {
                logger.Info(" => Persona Ids sequences are equal.");
            }
            else
            {
                logger.Info(" => Persona Ids sequences are not equal.");
                var numberOfPersonaIds = sortedPersonaIds.Count;
                var numberOfPersonaIdsForCalculatedRoute = sortedPersonaIdsForCalculatedRoutes.Count;

                var maxNumberOfItems = (numberOfPersonaIdsForCalculatedRoute >= numberOfPersonaIds)? numberOfPersonaIdsForCalculatedRoute : numberOfPersonaIds;
                var displayLimit = "";
                if(maxNumberOfItems>100)
                {
                    maxNumberOfItems = 100;
                    displayLimit = "(first " + maxNumberOfItems + " Ids)";
                }
                logger.Debug("{0,25} :: {1,25} {2}", "Sorted persona Ids", "Sorted persona Ids for calculated routes", displayLimit);
                for(int i = 0; i < maxNumberOfItems; i++)
                {
                    string personaId  = "(Empty)";
                    if(i < numberOfPersonaIds)
                        personaId = sortedPersonaIds[i].Id.ToString();
                    string personaIdForRoute = "(Empty)";
                    if(i < numberOfPersonaIdsForCalculatedRoute)
                        personaIdForRoute = sortedPersonaIdsForCalculatedRoutes[i].Id.ToString();
                    string nodeDifferenceMark = "";
                    if(!personaId.Equals(personaIdForRoute))
                        nodeDifferenceMark = "<<==";
                    logger.Debug("{0,25} :: {1,-25}\t\t{2}", personaId, personaIdForRoute, nodeDifferenceMark);
                }
            } 
        }

        public void QueueRoutingBenchmark(int totalElements, int processedElements, TimeSpan timeSpan, long timeSpanMilliseconds, int queueNumber)
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var elementProcessingRate = (double)processedElements / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var completionTimeEstimateSeconds = totalElements / elementProcessingRate;
            var completionTimeEstimate = TimeSpan.FromSeconds(completionTimeEstimateSeconds);

            var totalCompletionTimeEstimate = Helper.FormatElapsedTime(completionTimeEstimate);

            queueBenchmarks[queueNumber].PendingElements = PersonaQueues[queueNumber].Count;
            queueBenchmarks[queueNumber].ProcessedElements = processedElements;
            queueBenchmarks[queueNumber].ProcessingRate = elementProcessingRate;
            queueBenchmarks[queueNumber].ElapsedTime = elapsedTime;
            queueBenchmarks[queueNumber].ExpectedCompletionTime = totalCompletionTimeEstimate;

            var bestScoreQueue = GetBestScoreQueue();
            var worstScoreQueue = GetWorstScoreQueue();
            var averageEstimatedCompletionTime = GetAverageEstimatedCompletionTime();

            string queueString                     = "                     Queue # ::";
            string pendingElementString            = "            Pending elements ::";
            string processedElementString          = "          Processed elements ::";
            string processingRateString            = "Processing rate [routes / s] ::";
            string elapsedTimeString               = "                Elapsed time ::";
            string expectedCompletionTimeString    = "   Estimated completion time ::";
            string bestScoreString                 = "              Best (^) score ::";
            string worstScoreString                = "             Worst (~) score ::";
            for(int q =0; q < numberOfQueues; q++)
            {
                queueString += String.Format("{0,-15}", "\t" + q);
                pendingElementString += String.Format("{0,-15}", "\t" + queueBenchmarks[q].PendingElements);
                processedElementString += String.Format("{0,-15}", "\t" + queueBenchmarks[q].ProcessedElements);
                processingRateString += String.Format("{0,-15}", "\t" + queueBenchmarks[q].ProcessingRate.ToString("F", CultureInfo.InvariantCulture));
                elapsedTimeString += String.Format("{0,-15}", "\t" + queueBenchmarks[q].ElapsedTime);
                expectedCompletionTimeString += String.Format("{0,-15}", "\t" + queueBenchmarks[q].ExpectedCompletionTime);
                bestScoreString += String.Format("{0,-15}", "\t" + ((q==bestScoreQueue)? "^^^^^^^^^^^^":""));
                worstScoreString += String.Format("{0,-15}", "\t" + ((q==worstScoreQueue)? "~~~~~~~~~~~~":""));
            }
            
            logger.Debug(queueString);
            logger.Debug(pendingElementString);
            logger.Debug(processedElementString);
            logger.Debug(processingRateString);
            logger.Debug(elapsedTimeString);
            logger.Debug(expectedCompletionTimeString);
            logger.Debug(bestScoreString);
            logger.Debug(worstScoreString);
            logger.Debug("");
            logger.Debug("Average estimated completion time :: {0}", averageEstimatedCompletionTime);
            logger.Debug("");
        }

        public int GetBestScoreQueue()
        {
            int bestScoreQueue = 0;
            double bestProcessingRate = 0;
            for(var s = 0; s < queueBenchmarks.Count; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out QueueBenchmark? benchmark))
                {
                    if(benchmark.ProcessingRate > bestProcessingRate)
                    {
                        bestProcessingRate = benchmark.ProcessingRate;
                        bestScoreQueue = s;
                    }
                }
            }

            return bestScoreQueue;
        }

        public int GetWorstScoreQueue()
        {
            int worstScoreQueue = 0;
            double worstProcessingRate = Double.PositiveInfinity;
            for(var s = 0; s < queueBenchmarks.Count; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out QueueBenchmark? benchmark))
                {
                    if(benchmark.ProcessingRate < worstProcessingRate)
                    {
                        worstProcessingRate = benchmark.ProcessingRate;
                        worstScoreQueue = s;
                    }
                }
            }

            return worstScoreQueue;
        }

        public string GetAverageEstimatedCompletionTime()
        {
            var numberOfBenchmarks = queueBenchmarks.Count;
            TimeSpan timeStampAverage = TimeSpan.Zero;
            for(var s = 0; s < numberOfBenchmarks; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out QueueBenchmark? benchmark))
                {
                    if(benchmark.ExpectedCompletionTime is not null)
                    {
                        var timeStamp = TimeSpan.Parse(benchmark.ExpectedCompletionTime);
                        timeStampAverage += timeStamp;
                    }
                }
            }

            timeStampAverage = timeStampAverage / numberOfBenchmarks;

            return Helper.FormatElapsedTime(timeStampAverage);
        }
    }
}
