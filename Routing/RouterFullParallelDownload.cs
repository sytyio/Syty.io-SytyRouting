using NLog;
using System.Diagnostics;
using SytyRouting.Model;
using System.Collections.Concurrent;
using SytyRouting.Algorithms;
using Npgsql;
using System.Globalization;

namespace SytyRouting.Routing
{
    public class RouterFullParallelDownload : BaseRouter
    {
        private const int BatchesPerQueueDivisor = 1000;
        const int MinQueueWaitingTimeMilliseconds = 0_500;
        const int MaxQueueWaitingTimeMilliseconds = 5_000;


        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static int parallelTasks = Environment.ProcessorCount + 3; // +1 downloading task +1 uploading task +1 monitoring task
        private Task[] tasks = new Task[parallelTasks];
        private DataSetBenchmark dBSetBenchmark = null!;
        private bool Initialized = false;
        private bool PersonaDownloadEnded = false;
        private static ConcurrentQueue<Persona>[] PersonaQueues = new ConcurrentQueue<Persona>[parallelTasks-3];
        private ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks = new ConcurrentDictionary<int, DataSetBenchmark>(PersonaQueues.Count(), PersonaQueues.Count());
        private int numberOfBatches = 1; // At least one batch is expected (needs to be int for the thread lock mechanism to work)
        private static int stopRoutingProcess = 0; // 0 == DO NOT STOP; 1 == STOP;

        private ConcurrentQueue<Persona> routesQueue = new ConcurrentQueue<Persona>();


        

        public override void Initialize(Graph graph, string connectionString, string routeTable, string comparisonTable = "", string benchmarkTable = "")
        {
            _graph = graph;
            _connectionString = connectionString;
            _routeTable = routeTable;
            _comparisonTable = comparisonTable;
            _benchmarkTable = benchmarkTable;

            for (var i = 0; i < PersonaQueues.Length; i++)
            {
                PersonaQueues[i] = (PersonaQueues[i]) ?? new ConcurrentQueue<Persona>();
            }

            for (var i = 0; i < PersonaQueues.Count(); i++)
            {
                if(!queueBenchmarks.TryAdd(i, new DataSetBenchmark {Id = i}))
                    logger.Debug("Failed to initialize queues benchmarks at queue #{0}", i);
            }

            dBSetBenchmark = new DataSetBenchmark {Id = PersonaQueues.Count()};

            Initialized = true;
        }

        public override void Reset()
        {
            personas.Clear();
            Personas.Clear();

            ComputedRoutesCount = 0;
            TotalRoutingTime = TimeSpan.Zero;
            TotalUploadingTime = TimeSpan.Zero;
            TotalDownloadingTime = TimeSpan.Zero;

            baseRouterStopWatch.Reset();
            elementsToProcess = 0;
            computedRoutes = 0;
            processedDbElements = 0;
            uploadedRoutes = 0;
            routingTasksHaveEnded = false;
            personaTaskArraysQueue.Clear();
            originEqualsDestinationErrors = 0;

            PersonaDownloadEnded = false;
            stopRoutingProcess = 0; // 0 == DO NOT STOP; 1 == STOP;
        }

        public override async Task StartRouting<A,D,U>() //where A: IRoutingAlgorithm, D: IPersonaDownloader, U: IRouteUploader
        {
            if (Initialized == false)
            {
                logger.Info("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                logger.Info("!!   Full-parallel download router not initialized   !!");
                logger.Info("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return;
            }

            baseRouterStopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            elementsToProcess = await Helper.DbTableRowCount(_routeTable, logger);
            if(elementsToProcess < 1)
            {
                logger.Info("No DB elements to process");
                return;
            }
            else if(elementsToProcess < simultaneousRoutingTasks)
            {
                simultaneousRoutingTasks = elementsToProcess;
            }

            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            logger.Info(":  Starting Full-Parallel persona dowload process.  :");
            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");


            tasks[0] = Task.Run(() => DownloadPersonasAsync<D>());

            Thread.Sleep(initialDataLoadSleepMilliseconds);


            Stopwatch routingWatch = new Stopwatch();
            routingWatch.Start();

            for(int q = 1; q < tasks.Length-2; ++q)
            {
                var queueIdx = q;
                tasks[queueIdx] = Task.Run(() => CalculateRoutes<A,U>(queueIdx-1));
            }

            tasks[tasks.Length-2] = Task.Run(() => MonitorRouteCalculation());
            

            
            var routingTasksWaitArray = tasks[1..(tasks.Length-2)];
            Task.WaitAll(routingTasksWaitArray);
            routingWatch.Stop();
            var routingTime = Helper.FormatElapsedTime(routingWatch.Elapsed);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            logger.Info("      Routing time :: {0}", routingTime);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            TotalRoutingTime = routingWatch.Elapsed;

            routingTasksHaveEnded = true;

            
            tasks[tasks.Length-1] = Task.Run(() => UploadRoutesAsync<U>());

            
            Task.WaitAll(tasks);
            foreach (Task t in tasks)
            {
                Console.WriteLine("Task #{0} status: {1}", t.Id, t.Status);
            }
            

            ComputedRoutesCount = computedRoutes;
            Personas = personas;


            baseRouterStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(baseRouterStopWatch.Elapsed);
            logger.Info("=================================================");
            logger.Info("    Routing execution time :: {0}", totalTime);
            logger.Info("=================================================");

            TotalExecutionTime = baseRouterStopWatch.Elapsed;
        }

        protected override async Task DownloadPersonasAsync<D>()
        {
            Stopwatch downloadWatch = new Stopwatch();
            downloadWatch.Start();
            
            var downloader = new D();
            downloader.Initialize(_graph,_connectionString,_routeTable);

            //int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

            logger.Debug("> Thread #{0}\t=>\tDownloadPersonasAsync", Thread.CurrentThread.ManagedThreadId);
            logger.Debug("> DownloadPersonasAsync started");
            
            int[] batchSizes = GetBatchSizes();

            int dbRowsProcessed = 0;
            var currentQueue = 0;
            var offset = 0;

            var connectionString = Configuration.ConnectionString;

            //await using var connection = new NpgsqlConnection(connectionString);
            //await connection.OpenAsync();
            //connection.TypeMapper.UseNetTopologySuite();

            for(var b = 0; b < numberOfBatches; b++)
            {
                var batchSize = batchSizes[b];

                var personasArray = await downloader.DownloadPersonasAsync(connectionString,_routeTable,batchSize,offset);

                personas.AddRange(personasArray);
                foreach(var persona in personasArray)
                {
                    PersonaQueues[currentQueue].Enqueue(persona);
                    //personas.AddRange(personasArray); // <- Watch out here!
                }

                dbRowsProcessed += personasArray.Length;

                if (dbRowsProcessed % 5000 == 0)
                {
                    logger.Debug("Queue #{0}: {1} elements (batch #{2}: {3} elements)", currentQueue, PersonaQueues[currentQueue].Count, b, batchSize);
                    var timeSpan = downloadWatch.Elapsed;
                    var timeSpanMilliseconds = downloadWatch.ElapsedMilliseconds;
                    var result = Helper.DataLoadBenchmark(elementsToProcess, dbRowsProcessed, timeSpan, logger);
                    DBLoadBenchmark(result);
                }


                offset = offset + batchSize;
                currentQueue = ChangeQueue(currentQueue);
            }

            //await connection.CloseAsync();
            
            PersonaDownloadEnded = true;

            var sequenceValidationErrors = downloader.GetValidationErrors();
            logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);

            downloadWatch.Stop();
            var downloadTime = Helper.FormatElapsedTime(downloadWatch.Elapsed);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
            logger.Info("  Persona download time :: {0}", downloadTime);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
            TotalDownloadingTime = downloadWatch.Elapsed;


            dBSetBenchmark.PendingElements = elementsToProcess - dbRowsProcessed;
            dBSetBenchmark.ProcessedElements = dbRowsProcessed;
            dBSetBenchmark.ElapsedTime = downloadTime;
            dBSetBenchmark.ExpectedCompletionTime = Helper.FormatElapsedTime(TimeSpan.Zero);

            logger.Info("                           Persona set creation time :: " + downloadTime);
            logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, elementsToProcess);
            logger.Debug("Number of Queues: {0}", PersonaQueues.Count());
            logger.Debug("Total number of elements in queues: {0}", elementsToProcess);

            logger.Debug("> DownloadPersonasAsync ended");
        }

        private int[] GetBatchSizes()
        {
            int numberOfBatchesPerQueue = (elementsToProcess / PersonaQueues.Count() / BatchesPerQueueDivisor)>0 ? elementsToProcess / PersonaQueues.Count() / BatchesPerQueueDivisor : 1;
            numberOfBatches = PersonaQueues.Count() * numberOfBatchesPerQueue;
            
            var regularBatchSize = elementsToProcess / numberOfBatches;
            var lastBatchSize = elementsToProcess - regularBatchSize * (numberOfBatches-1);

            int[] batchSizes = new int[numberOfBatches];

            for (var i = 0; i < batchSizes.Length-1; i++)
            {
                batchSizes[i] = regularBatchSize;
            }
            batchSizes[batchSizes.Length-1] = lastBatchSize;

            return batchSizes;
        }

        private void DBLoadBenchmark(DataSetBenchmark benchmark)
        {
            dBSetBenchmark.PendingElements = benchmark.PendingElements;
            dBSetBenchmark.ProcessedElements = benchmark.ProcessedElements;
            dBSetBenchmark.ProcessingRate = benchmark.ProcessingRate;
            dBSetBenchmark.ElapsedTime = benchmark.ElapsedTime;
            dBSetBenchmark.ExpectedCompletionTime = benchmark.ExpectedCompletionTime;
        }

        private int ChangeQueue(int currentQueue)
        {
            return (++currentQueue >= PersonaQueues.Count()) ? 0 : currentQueue;
        }

        protected override void CalculateRoutes<A,U>(int queueNumber) //where A: IRoutingAlgorithm, U: IRouteUploader
        {   
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var routingAlgorithm = new A();
            routingAlgorithm.Initialize(_graph);

            int calculatedRoutes = 0;

            logger.Debug("> Thread #{0}\t=>\tQueue #{1}", Thread.CurrentThread.ManagedThreadId, queueNumber);
            logger.Debug("> CalculateRoutes started for Queue #{0}", queueNumber);
            while(stopRoutingProcess != 1)
            {
                if(PersonaQueues[queueNumber].TryDequeue(out Persona? persona))
                {
                    try
                    {
                        var routeFound = CalculateRoute(routingAlgorithm, ref persona);
                        
                        if(routeFound)
                        {
                            routesQueue.Enqueue(persona);
                            Interlocked.Increment(ref computedRoutes);
                        }
                    }
                    catch (Exception e)
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                    }
            
                    //personasWithRoute.Add(persona);
                    calculatedRoutes++;

                    if(elementsToProcess == computedRoutes)
                    {
                        Interlocked.Exchange(ref stopRoutingProcess, 1);
                        routingTasksHaveEnded = true;
                    }

                    if (calculatedRoutes % 100 == 0)
                    {
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        QueueRoutingBenchmark(elementsToProcess / PersonaQueues.Count(), calculatedRoutes, PersonaQueues[queueNumber].Count, timeSpan, timeSpanMilliseconds, queueNumber, ref queueBenchmarks, ref dBSetBenchmark);
                    }
                }
                else if(PersonaDownloadEnded)
                {
                    logger.Debug("> DB download has ended. No more expected elements to process");
                    break;
                }
                else
                {
                    int expectedDbSetCompletionTime = 0; // [ms]
                    if(dBSetBenchmark.ExpectedCompletionTime is not null)
                    {
                        expectedDbSetCompletionTime = Convert.ToInt32(TimeSpan.Parse(dBSetBenchmark.ExpectedCompletionTime).TotalMilliseconds);
                    }
                    var pause = (expectedDbSetCompletionTime / numberOfBatches) > 0 ? expectedDbSetCompletionTime / numberOfBatches : MinQueueWaitingTimeMilliseconds;
                    if(pause > MaxQueueWaitingTimeMilliseconds)
                    {
                        logger.Debug("Queue #{0} will wait for {1} [ms]", queueNumber, pause);
                    }
                    Thread.Sleep(pause);
                }
            }
            logger.Debug("> CalculateRoutes ended for Queue #{0}", queueNumber);
        }

        private static void QueueRoutingBenchmark(int totalElements, int processedElements, int pendingElements, TimeSpan timeSpan, long timeSpanMilliseconds,
                                                    int queueNumber, ref ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks, ref DataSetBenchmark dBSetBenchmark)
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var elementProcessingRate = (double)processedElements / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var completionTimeEstimateSeconds = totalElements / elementProcessingRate;
            var completionTimeEstimate = TimeSpan.FromSeconds(completionTimeEstimateSeconds);

            var totalCompletionTimeEstimate = Helper.FormatElapsedTime(completionTimeEstimate);

            queueBenchmarks[queueNumber].PendingElements = pendingElements;
            queueBenchmarks[queueNumber].ProcessedElements = processedElements;
            queueBenchmarks[queueNumber].ProcessingRate = elementProcessingRate;
            queueBenchmarks[queueNumber].ElapsedTime = elapsedTime;
            queueBenchmarks[queueNumber].ExpectedCompletionTime = totalCompletionTimeEstimate;

            var bestScoreQueue = GetBestScoreQueue(ref queueBenchmarks);
            var worstScoreQueue = GetWorstScoreQueue(ref queueBenchmarks);
            var averageEstimatedCompletionTime = GetAverageEstimatedCompletionTime(ref queueBenchmarks);

            string queueString                     = "                       Queue # ::";
            string pendingElementString            = "              Pending elements ::";
            string processedElementString          = "            Processed elements ::";
            string processingRateString            = "Processing rate [elements / s] ::";
            string elapsedTimeString               = "                  Elapsed time ::";
            string expectedCompletionTimeString    = "     Estimated completion time ::";
            string bestScoreString                 = "                Best (^) score ::";
            string worstScoreString                = "               Worst (~) score ::";
            string baseString = "\t{0,-18}";
            for(int q =0; q < PersonaQueues.Count(); q++)
            {
                queueString += String.Format(baseString, q);
                pendingElementString += String.Format(baseString, queueBenchmarks[q].PendingElements);
                processedElementString += String.Format(baseString, queueBenchmarks[q].ProcessedElements);
                processingRateString += String.Format(baseString, queueBenchmarks[q].ProcessingRate.ToString("F", CultureInfo.InvariantCulture));
                elapsedTimeString += String.Format(baseString, queueBenchmarks[q].ElapsedTime);
                expectedCompletionTimeString += String.Format(baseString, queueBenchmarks[q].ExpectedCompletionTime);
                bestScoreString += String.Format(baseString, ((q==bestScoreQueue)? "^^^^^^^^^^^^":""));
                worstScoreString += String.Format(baseString, ((q==worstScoreQueue)? "~~~~~~~~~~~~":""));
            }

            queueString                  += "\t||" + String.Format(baseString, "Persona data set");
            pendingElementString         += "\t||" + String.Format(baseString, dBSetBenchmark.PendingElements)                                            + " :: Pending elements";
            processedElementString       += "\t||" + String.Format(baseString, dBSetBenchmark.ProcessedElements)                                          + " :: Processed elements";
            processingRateString         += "\t||" + String.Format(baseString, dBSetBenchmark.ProcessingRate.ToString("F", CultureInfo.InvariantCulture)) + " :: Processing rate [elements / s]";
            elapsedTimeString            += "\t||" + String.Format(baseString, dBSetBenchmark.ElapsedTime)                                                + " :: Elapsed time";
            expectedCompletionTimeString += "\t||" + String.Format(baseString, dBSetBenchmark.ExpectedCompletionTime)                                     + " :: Estimated completion time";
            
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

        private static int GetBestScoreQueue(ref ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks)
        {
            int bestScoreQueue = 0;
            double bestProcessingRate = 0;
            for(var s = 0; s < queueBenchmarks.Count; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
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

        private static int GetWorstScoreQueue(ref ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks)
        {
            int worstScoreQueue = 0;
            double worstProcessingRate = Double.PositiveInfinity;
            for(var s = 0; s < queueBenchmarks.Count; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
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

        private static string GetAverageEstimatedCompletionTime(ref ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks)
        {
            var numberOfBenchmarks = queueBenchmarks.Count;
            TimeSpan timeStampAverage = TimeSpan.Zero;
            for(var s = 0; s < numberOfBenchmarks; ++s)
            {
                if(queueBenchmarks.TryGetValue(s, out DataSetBenchmark? benchmark))
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

        protected override async Task UploadRoutesAsync<U>()// where U: IRouteUploader
        {
            Stopwatch uploadWatch = new Stopwatch();
            uploadWatch.Start();

            var uploader = new U();
            await uploader.UploadRoutesAsync(_connectionString,_routeTable,personas,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

            uploadWatch.Stop();
            var downloadTime = Helper.FormatElapsedTime(uploadWatch.Elapsed);
            logger.Info("UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU");
            logger.Info("  Persona upload time :: {0}", downloadTime);
            logger.Info("UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU");
            TotalUploadingTime = uploadWatch.Elapsed;
            
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
        }

        // protected override async Task UploadRoutesAsync<U>()// where U: IRouteUploader, new()
        // {
        //     Stopwatch uploadStopWatch = new Stopwatch();

        //     var uploader = new U();

        //     var uploadBatchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;

        //     List<Persona> uploadBatch = new List<Persona>(uploadBatchSize);
        //     int uploadFails = 0;
        //     int uploadedRoutes = 0;

        //     int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
        //     while(true)
        //     {
        //         logger.Debug("{0} elements in the uploading queue",routesQueue.Count);
        //         if(routesQueue.Count>=uploadBatchSize)
        //         {
        //             uploadStopWatch.Start();
        //             while(uploadBatch.Count<=uploadBatchSize && routesQueue.TryDequeue(out Persona? persona))
        //             {
        //                 if(persona!=null)
        //                 {
        //                     uploadBatch.Add(persona);
        //                 }
        //             }
        //             logger.Debug("Uploading {0} routes",uploadBatch.Count);
                    
        //             await uploader.UploadRoutesAsync(_connectionString,_routeTable,uploadBatch,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

        //             uploadedRoutes += uploadBatch.Count - uploadFails;
        //             logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
        //             uploadBatch.Clear();
        //             uploadStopWatch.Stop();
        //         }

        //         if(routingTasksHaveEnded)
        //         {
        //             uploadStopWatch.Start();

        //             var remainingRoutes = routesQueue.ToList();

        //             logger.Debug("Routing tasks have ended. Computed routes queue dump. Uploading {0} remaining routes",remainingRoutes.Count);
                    
        //             await uploader.UploadRoutesAsync(_connectionString,_routeTable,remainingRoutes,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

        //             uploadedRoutes += remainingRoutes.Count - uploadFails;
        //             logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                
        //             uploadStopWatch.Stop();
        //             TotalUploadingTime = uploadStopWatch.Elapsed;
        //             var totalTime = Helper.FormatElapsedTime(TotalUploadingTime);
        //             logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", uploadedRoutes, _comparisonTable, totalTime);
        //             logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, uploadBatch.Count, 100.0 * (double)uploadFails / (double)uploadBatch.Count);
        //             logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)uploadBatch.Count);
        //             logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)uploadBatch.Count);
             
        //             return;
        //         }

        //         Thread.Sleep(monitorSleepMilliseconds);
        //     }
        // }
    }
}
