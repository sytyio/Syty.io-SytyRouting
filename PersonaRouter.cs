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

        private static int numberOfRouters = Environment.ProcessorCount; // 8-core computer;
        private static int numberOfBatchesPerRouter = 10;
        private int numberOfBatches = numberOfRouters * numberOfBatchesPerRouter;
    

        private static int stopProcess = 0; // 0 == DO NOT STOP; 1 == STOP;
        private static int elementsToProcess = 0;
        private static int processedElements = 0;
    

        private Task[] tasks = new Task[numberOfRouters + 1]; // #queues + 1 data load task

        private ConcurrentDictionary<int, DataSetBenchmark> routerBenchmarks = new ConcurrentDictionary<int, DataSetBenchmark>(numberOfRouters, numberOfRouters); // For testing
        private DataSetBenchmark dBSetBenchmark;
        
        private ConcurrentQueue<Persona> PersonaQueues = new ConcurrentQueue<Persona>();
        ConcurrentBag<Persona> personaIdsForCalculatedRoutes = new ConcurrentBag<Persona>(); // For degugging


        public PersonaRouter(Graph graph)
        {
            _graph = graph;

            PersonaQueues = (PersonaQueues) ?? new ConcurrentQueue<Persona>();

            for (var i = 0; i < numberOfRouters; i++)
            {
                if(!routerBenchmarks.TryAdd(i, new DataSetBenchmark {Id = i}))
                    logger.Debug("Failed to initialize benchmark for router #{0}", i);
            }
            dBSetBenchmark = new DataSetBenchmark {Id = numberOfRouters};
        }

        public async Task StartRouting<T>() where T: IRoutingAlgorithm, new()
        {
            logger.Debug("> StartRouting initiated");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var routingAlgorithms = new IRoutingAlgorithm[numberOfRouters];
            for(var a = 0; a < routingAlgorithms.Length; a++)
            {
                routingAlgorithms[a] = new T();
                routingAlgorithms[a].Initialize(_graph);
            }
            logger.Info("Route searching using {0}'s algorithm on {1} parallel routing queue(s)", routingAlgorithms[0].GetType().Name, numberOfRouters);
            

            var connectionString = Constants.ConnectionString;
            var tableName = "public.persona";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            // elementsToProcess = await Helper.DbTableRowCount(connection, tableName, logger);
            elementsToProcess = 1000;

            await connection.CloseAsync();

            // ///////////////////////////////////////////////////////////////// //
            // Queues load and queue routing in parallel  (Single persona queue) //
            // ///////////////////////////////////////////////////////////////// //
            
            tasks[0] = DBPersonaLoadAsync(elementsToProcess);

            for(var t = 1; t < tasks.Length; t++)
            {
                var tdx = t;
                tasks[tdx] = Task.Run(() => CalculateRoutes(routingAlgorithms[tdx-1], tdx-1));
            }

           

            Task.WaitAll(tasks);
            foreach (Task t in tasks)
                Console.WriteLine("Task #{0} status: {1}", t.Id, t.Status);

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
            logger.Info("StartRouting execution time using {0} algorithm :: {1}",routingAlgorithms[0].GetType().Name, totalTime);

            logger.Debug("> StartRouting ended");
        }

        private async Task DBPersonaLoadAsync(int totalDbRows)
        {
            logger.Debug("> Thread #{0}\t=>\tDBPersonaLoadAsync", Thread.CurrentThread.ManagedThreadId);
            logger.Debug("> DBPersonaLoadAsync started");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var connectionString = Constants.ConnectionString;
            var tableName = "public.persona";

            
            var regularBatchSize = totalDbRows / numberOfBatches;
            var lastBatchSize = regularBatchSize + totalDbRows % numberOfBatches;


            int[] batchSizes = new int[numberOfBatches];
            for (var i = 0; i < batchSizes.Length-1; i++)
            {
                batchSizes[i] = regularBatchSize;
            }
            batchSizes[batchSizes.Length-1] = lastBatchSize;


            int dbRowsProcessed = 0;
            
            var offset = 0;

            for(var b = 0; b < numberOfBatches; b++)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                connection.TypeMapper.UseNetTopologySuite();

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

                        CreatePersona(id, homeLocation, workLocation);

                        dbRowsProcessed++;

                        if (dbRowsProcessed % 50_000 == 0)
                        {
                            logger.Debug("{0} elements in PersonaQueue (batch #{1})", PersonaQueues.Count, b);
                            var timeSpan = stopWatch.Elapsed;
                            var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                            var t = Task.Run(() => PersonaDataProcessingBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, ref dBSetBenchmark));
                        }
                    }
                }
                offset = offset + batchSizes[b];

                await connection.CloseAsync();
            }


            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);

            dBSetBenchmark.PendingElements = totalDbRows - dbRowsProcessed;
            dBSetBenchmark.ProcessedElements = dbRowsProcessed;
            dBSetBenchmark.ElapsedTime = totalTime;
            dBSetBenchmark.ExpectedCompletionTime = Helper.FormatElapsedTime(TimeSpan.Zero);

            logger.Info("                           Persona set creation time :: " + totalTime);
            logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, totalDbRows);
            logger.Debug("Total number of elements to process: {0}", elementsToProcess);

            logger.Debug("> DBPersonaLoadAsync ended");
        }

        private void CreatePersona(int id, Point homeLocation, Point workLocation)
        {   
            var persona = new Persona { Id = id, HomeLocation = homeLocation, WorkLocation = workLocation };
            PersonaQueues.Enqueue(persona);
        }

        private void CalculateRoutes(IRoutingAlgorithm routingAlgorithm, int routerNumber)
        {   
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            int calculatedRoutes = 0;

            logger.Debug("> Thread #{0}\t++++>\tRouter #{1}", Thread.CurrentThread.ManagedThreadId, routerNumber);
            logger.Debug("> CalculateRoutes started for Router #{0}", routerNumber);
            while(stopProcess != 1)
            {
                if(PersonaQueues.TryDequeue(out Persona? persona))
                {
                    var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                    var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                    var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);
            
                    personaIdsForCalculatedRoutes.Add(persona);
                    calculatedRoutes++;

                    if(elementsToProcess == Interlocked.Increment(ref processedElements))
                    {
                        Interlocked.Exchange(ref stopProcess, 1);
                        logger.Debug("Stop Routing singnal generated by Router #{0}", routerNumber);
                    }

                    if (calculatedRoutes % 100 == 0)
                    {
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        Task.Run(() => RoutingBenchmark(elementsToProcess / numberOfRouters, calculatedRoutes, PersonaQueues.Count, timeSpan, timeSpanMilliseconds, routerNumber, ref routerBenchmarks, ref dBSetBenchmark));
                    }
                }
                else
                {
                    logger.Debug("Router #{0} is idle", routerNumber);
                    Thread.Sleep(1000);
                }
            }
            logger.Debug("> CalculateRoutes ended for Router #{0}", routerNumber);
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

        private static void PersonaDataProcessingBenchmark(int totalDbRows, int dbRowsProcessed, TimeSpan timeSpan, long timeSpanMilliseconds, ref DataSetBenchmark dBSetBenchmark)
        {
            var result = Helper.DataLoadBenchmark(totalDbRows, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
            dBSetBenchmark.PendingElements = result.PendingElements;
            dBSetBenchmark.ProcessedElements = result.ProcessedElements;
            dBSetBenchmark.ProcessingRate = result.ProcessingRate;
            dBSetBenchmark.ElapsedTime = result.ElapsedTime;
            dBSetBenchmark.ExpectedCompletionTime = result.ExpectedCompletionTime;
        }

        private static void RoutingBenchmark(int totalElements, int processedElements, int pendingElements, TimeSpan timeSpan, long timeSpanMilliseconds,
                                                    int routerNumber, ref ConcurrentDictionary<int, DataSetBenchmark> routerBenchmarks, ref DataSetBenchmark dBSetBenchmark)                                                    
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var elementProcessingRate = (double)processedElements / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var completionTimeEstimateSeconds = totalElements / elementProcessingRate;
            var completionTimeEstimate = TimeSpan.FromSeconds(completionTimeEstimateSeconds);

            var totalCompletionTimeEstimate = Helper.FormatElapsedTime(completionTimeEstimate);

            routerBenchmarks[routerNumber].PendingElements = pendingElements;
            routerBenchmarks[routerNumber].ProcessedElements = processedElements;
            routerBenchmarks[routerNumber].ProcessingRate = elementProcessingRate;
            routerBenchmarks[routerNumber].ElapsedTime = elapsedTime;
            routerBenchmarks[routerNumber].ExpectedCompletionTime = totalCompletionTimeEstimate;

            var bestScoreRouter = GetBestScoreRouter(ref routerBenchmarks);
            var worstScoreRouter = GetWorstScoreRouter(ref routerBenchmarks);
            var averageEstimatedCompletionTime = GetAverageEstimatedCompletionTime(ref routerBenchmarks);

            string headerString                    = "                      Router # ::";
            string pendingElementString            = "              Pending elements ::";
            string processedElementString          = "            Processed elements ::";
            string processingRateString            = "Processing rate [elements / s] ::";
            string elapsedTimeString               = "                  Elapsed time ::";
            string expectedCompletionTimeString    = "     Estimated completion time ::";
            string bestScoreString                 = "                Best (^) score ::";
            string worstScoreString                = "               Worst (~) score ::";
            for(int q =0; q < numberOfRouters; q++)
            {
                headerString += String.Format("{0,-15}", "\t" + q);
                pendingElementString += String.Format("{0,-15}", "\t" + routerBenchmarks[q].PendingElements);
                processedElementString += String.Format("{0,-15}", "\t" + routerBenchmarks[q].ProcessedElements);
                processingRateString += String.Format("{0,-15}", "\t" + routerBenchmarks[q].ProcessingRate.ToString("F", CultureInfo.InvariantCulture));
                elapsedTimeString += String.Format("{0,-15}", "\t" + routerBenchmarks[q].ElapsedTime);
                expectedCompletionTimeString += String.Format("{0,-15}", "\t" + routerBenchmarks[q].ExpectedCompletionTime);
                bestScoreString += String.Format("{0,-15}", "\t" + ((q==bestScoreRouter)? "^^^^^^^^^^^^":""));
                worstScoreString += String.Format("{0,-15}", "\t" + ((q==worstScoreRouter)? "~~~~~~~~~~~~":""));
            }

            headerString                  += "\t||" + String.Format("{0,-15}", "\t" + "persona data set");
            pendingElementString         += "\t||" + String.Format("{0,-15}", "\t" + dBSetBenchmark.PendingElements);
            processedElementString       += "\t||" + String.Format("{0,-15}", "\t" + dBSetBenchmark.ProcessedElements);
            processingRateString         += "\t||" + String.Format("{0,-15}", "\t" + dBSetBenchmark.ProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            elapsedTimeString            += "\t||" + String.Format("{0,-15}", "\t" + dBSetBenchmark.ElapsedTime);
            expectedCompletionTimeString += "\t||" + String.Format("{0,-15}", "\t" + dBSetBenchmark.ExpectedCompletionTime);
            
            logger.Debug(headerString);
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

        private static int GetBestScoreRouter(ref ConcurrentDictionary<int, DataSetBenchmark> routerBenchmarks)
        {
            int bestScoreRouter = 0;
            double bestProcessingRate = 0;
            for(var s = 0; s < routerBenchmarks.Count; ++s)
            {
                if(routerBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
                {
                    if(benchmark.ProcessingRate > bestProcessingRate)
                    {
                        bestProcessingRate = benchmark.ProcessingRate;
                        bestScoreRouter = s;
                    }
                }
            }

            return bestScoreRouter;
        }

        private static int GetWorstScoreRouter(ref ConcurrentDictionary<int, DataSetBenchmark> routerBenchmarks)
        {
            int worstScoreRouter = 0;
            double worstProcessingRate = Double.PositiveInfinity;
            for(var s = 0; s < routerBenchmarks.Count; ++s)
            {
                if(routerBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
                {
                    if(benchmark.ProcessingRate < worstProcessingRate)
                    {
                        worstProcessingRate = benchmark.ProcessingRate;
                        worstScoreRouter = s;
                    }
                }
            }

            return worstScoreRouter;
        }

        private static string GetAverageEstimatedCompletionTime(ref ConcurrentDictionary<int, DataSetBenchmark> routerBenchmarks)
        {
            var numberOfBenchmarks = routerBenchmarks.Count;
            TimeSpan timeStampAverage = TimeSpan.Zero;
            for(var s = 0; s < numberOfBenchmarks; ++s)
            {
                if(routerBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
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
