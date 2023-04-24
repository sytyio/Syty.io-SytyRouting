using NLog;
using System.Diagnostics;
using SytyRouting.Model;
using System.Collections.Concurrent;
using SytyRouting.Algorithms;

namespace SytyRouting.Routing
{
    public class RouterFullParallel : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static int parallelTasks = Environment.ProcessorCount + 2; // +1 downloading task +1 uploading task
        private Task[] tasks = new Task[parallelTasks];
        private DataSetBenchmark dBSetBenchmark = null!;
        private bool Initialized = false;
        private static ConcurrentQueue<Persona>[] PersonaQueues = new ConcurrentQueue<Persona>[parallelTasks-2];
        private ConcurrentDictionary<int, DataSetBenchmark> queueBenchmarks = new ConcurrentDictionary<int, DataSetBenchmark>(PersonaQueues.Count(), PersonaQueues.Count());

        

        public void Initialize()
        {
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
        public override async Task StartRouting<A,D,U>() //where A: IRoutingAlgorithm, D: IPersonaDownloader, U: IRouteUploader
        {
            baseRouterStopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            //elementsToProcess = await Helper.DbTableRowCount(_routeTable, logger);
            elementsToProcess = 6; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
            
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

            Stopwatch downloadWatch = new Stopwatch();
            downloadWatch.Start();

            tasks[0] = DownloadPersonasAsync<D>();
            for(int q = 1; q < tasks.Length; ++q)
            {
                var queueIdx = q;
                tasks[queueIdx] = Task.Run(() => CalculateRoutesForQueue<T>(queueIdx-1));
            }

            Task.WaitAll(tasks);
            foreach (Task t in tasks)
                Console.WriteLine("Task #{0} status: {1}", t.Id, t.Status);


            
            Task downloadTask = Task.Run(() => DownloadPersonasAsync<D>());
            Task.WaitAll(downloadTask);

            downloadWatch.Stop();
            var downloadTime = Helper.FormatElapsedTime(downloadWatch.Elapsed);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
            logger.Info("  Persona download time :: {0}", downloadTime);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");



            //Thread.Sleep(initialDataLoadSleepMilliseconds);
            if(personaTaskArraysQueue.Count < simultaneousRoutingTasks)
            {
                logger.Info(" ==>> Initial DB load timeout ({0} ms) elapsed. Unable to start the routing process.", initialDataLoadSleepMilliseconds);
                return;
            }
            
            for(int taskIndex = 0; taskIndex < routingTasks.Length; taskIndex++)
            {
                int t = taskIndex;
                routingTasks[t] = Task.Run(() => CalculateRoutes<A,U>(t));
            }
            Task monitorTask = Task.Run(() => MonitorRouteCalculation());


            Task.WaitAll(routingTasks);


            TotalDownloadingTime = downloadWatch.Elapsed;
            TotalRoutingTime = baseRouterStopWatch.Elapsed;

            routingTasksHaveEnded = true;
            
            Task.WaitAll(monitorTask);

            ComputedRoutesCount = computedRoutes;
            Personas = personas;

            await UploadRoutesAsync<U>();

            baseRouterStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(baseRouterStopWatch.Elapsed);
            logger.Info("=================================================");
            logger.Info("    Routing execution time :: {0}", totalTime);
            logger.Info("=================================================");
        }

        protected override async Task DownloadPersonasAsync<D>()
        {
            var downloader = new D();
            downloader.Initialize(_graph,_connectionString,_routeTable);

            //int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

            logger.Debug("> Thread #{0}\t=>\tDBPersonaLoadAsync", Thread.CurrentThread.ManagedThreadId);
            logger.Debug("> DownloadPersonasAsync started");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            int[] batchSizes = GetBatchSizes();

            int dbRowsProcessed = 0;
            var currentQueue = 0;
            var offset = 0;

            // var connectionString = Constants.ConnectionString;
            var connectionString = Constants.W505ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            connection.TypeMapper.UseNetTopologySuite();

            for(var b = 0; b < numberOfBatches; b++)
            {
                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                     0              1              2
                var queryString = "SELECT id, home_location, work_location FROM " + TableName + " ORDER BY id ASC LIMIT " + batchSizes[b] + " OFFSET " + offset;

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

                        if (dbRowsProcessed % 5000 == 0)
                        {
                            logger.Debug("Queue #{0}: {1} elements (batch #{2}: {3} elements)", currentQueue, PersonaQueues[currentQueue].Count, b, batchSizes[b]);
                            var timeSpan = stopWatch.Elapsed;
                            var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                            var result = Helper.DataLoadBenchmark(elementsToProcess, dbRowsProcessed, timeSpan, timeSpanMilliseconds, logger);
                            DBLoadBenchmark(result);
                        }
                    }
                }
                offset = offset + batchSizes[b];
                currentQueue = ChangeQueue(currentQueue);
            }

            await connection.CloseAsync();
            dBLoadHasEnded = true;
            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);

            dBSetBenchmark.PendingElements = elementsToProcess - dbRowsProcessed;
            dBSetBenchmark.ProcessedElements = dbRowsProcessed;
            dBSetBenchmark.ElapsedTime = totalTime;
            dBSetBenchmark.ExpectedCompletionTime = Helper.FormatElapsedTime(TimeSpan.Zero);

            logger.Info("                           Persona set creation time :: " + totalTime);
            logger.Debug("Number of DB rows processed: {0} (of {1})", dbRowsProcessed, elementsToProcess);
            logger.Debug("Number of Queues: {0}", numberOfQueues);
            logger.Debug("Total number of elements in queues: {0}", elementsToProcess);

            logger.Debug("> DownloadPersonasAsync ended");




















            int[] batchSizes = downloader.GetBatchSizes(regularBatchSize,elementsToProcess);

            int offset = 0;
            for(var batchNumber = 0; batchNumber < batchSizes.Length; batchNumber++)
            {
                var currentBatchSize = batchSizes[batchNumber];

                var routingTaskBatchSize = (currentBatchSize / simultaneousRoutingTasks > 0) ? currentBatchSize / simultaneousRoutingTasks : 1;
                int[] routingTaskBatchSizes = downloader.GetBatchPartition(routingTaskBatchSize, currentBatchSize, simultaneousRoutingTasks);                

                foreach(var routingBatchSize in routingTaskBatchSizes)
                {
                    Persona[] personaTaskArray = await downloader.DownloadPersonasAsync(_connectionString,_routeTable,routingBatchSize,offset);///

                    personaTaskArraysQueue.Enqueue(personaTaskArray);
                    personas.AddRange(personaTaskArray);

                    processedDbElements+=personaTaskArray.Length;

                    offset += routingBatchSize;
                }

                //while(personaTaskArraysQueue.Count > taskArraysQueueThreshold)
                //    Thread.Sleep(dBPersonaLoadAsyncSleepMilliseconds);
            }

            var sequenceValidationErrors = downloader.GetValidationErrors();
            logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);
        }

        protected override void CalculateRoutes<A,U>(int taskIndex) //where A: IRoutingAlgorithm, U: IRouteUploader
        {
            var routingAlgorithm = new A();
            routingAlgorithm.Initialize(_graph);
            
            while(personaTaskArraysQueue.TryDequeue(out Persona[]? personaArray))
            {
                for(var i = 0; i < personaArray.Length; i++)
                {
                    var persona = personaArray[i];

                    try
                    {
                        var routeFound = CalculateRoute(routingAlgorithm, ref persona);
                        
                        if(routeFound)
                        {
                            Interlocked.Increment(ref computedRoutes);
                        }
                    }
                    catch (Exception e)
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                    }
                }
            }
        }





        private void CalculateRoutesForQueue<T>(int queueNumber) where T: IRoutingAlgorithm, new()
        {   
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var routingAlgorithm = new T();
            routingAlgorithm.Initialize(_graph);

            int calculatedRoutes = 0;

            logger.Debug("> Thread #{0}\t=>\tQueue #{1}", Thread.CurrentThread.ManagedThreadId, queueNumber);
            logger.Debug("> CalculateRoutesForQueue started for Queue #{0}", queueNumber);
            while(stopRoutingProcess != 1)
            {
                if(PersonaQueues[queueNumber].TryDequeue(out Persona? persona))
                {
                    try
                    {
                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y);
                        var route = routingAlgorithm.GetRoute(origin.OsmID, destination.OsmID);
                        persona.Route = route.ToList();
                        persona.SuccessfulRouteComputation = true;
                    }
                    catch
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Info(" ==>> Unable to compute route: Persona Id {0}", persona.Id);
                    }
            
                    personasWithRoute.Add(persona);
                    calculatedRoutes++;

                    if(elementsToProcess == Interlocked.Increment(ref processedElements))
                    {
                        Interlocked.Exchange(ref stopRoutingProcess, 1);
                    }

                    if (calculatedRoutes % 100 == 0)
                    {
                        var timeSpan = stopWatch.Elapsed;
                        var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                        QueueRoutingBenchmark(elementsToProcess / numberOfQueues, calculatedRoutes, PersonaQueues[queueNumber].Count, timeSpan, timeSpanMilliseconds, queueNumber, ref queueBenchmarks, ref dBSetBenchmark);
                    }
                }
                else if(dBLoadHasEnded)
                {
                    logger.Debug("> DB load has ended. No more expected elements to process");
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
            logger.Debug("> CalculateRoutesForQueue ended for Queue #{0}", queueNumber);
        }









        protected override async Task UploadRoutesAsync<U>()// where U: IRouteUploader
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            var uploader = new U();

            await uploader.UploadRoutesAsync(_connectionString,_routeTable,personas,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

            uploadStopWatch.Stop();
            TotalUploadingTime = uploadStopWatch.Elapsed;
            var totalTime = Helper.FormatElapsedTime(TotalUploadingTime);
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
        }
    }
}
