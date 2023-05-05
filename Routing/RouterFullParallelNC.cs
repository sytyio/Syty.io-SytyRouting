using NLog;
using System.Diagnostics;
using SytyRouting.Model;
using System.Collections.Concurrent;
using SytyRouting.Algorithms;
using Npgsql;
using System.Globalization;
using SytyRouting.DataBase;

namespace SytyRouting.Routing
{
    public class RouterFullParallelNonConcurrent : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private DataSetBenchmark dBSetBenchmark = null!;
        private bool Initialized = false;
        private int numberOfBatches = 1; // At least one batch is expected (needs to be int for the thread lock mechanism to work)

        private IRoutingAlgorithm[] routingAlgorithms = new IRoutingAlgorithm[simultaneousRoutingTasks];
        private IRouteUploader[] routeUploaders = new IRouteUploader[simultaneousRoutingTasks];

        

        public override void Initialize(Graph graph, string connectionString, string routeTable, string comparisonTable = "", string benchmarkTable = "")
        {
            _graph = graph;
            _connectionString = connectionString;
            _routeTable = routeTable;
            _comparisonTable = comparisonTable;
            _benchmarkTable = benchmarkTable;

            for(var t = 0; t < routingTasks.Length; t++)
            {
                routingTasks[t] = Task.CompletedTask;
            }
            dBSetBenchmark = new DataSetBenchmark {Id = 0};

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

            //PersonaDownloadEnded = false;
            //stopRoutingProcess = 0; // 0 == DO NOT STOP; 1 == STOP;
        }

        private void InitializeRoutingAlgorithms<A>() where A: IRoutingAlgorithm, new()
        {
            for(var a = 0; a < routingAlgorithms.Length; a++)
            {
                routingAlgorithms[a] = new A();
                routingAlgorithms[a].Initialize(_graph);
                logger.Info("Route searching using {0}'s algorithm running on {1} simultaneous routing task(s)", routingAlgorithms[a].GetType().Name, simultaneousRoutingTasks);
            }
        }

        private void InitializeRouteUploaders<U>() where U: IRouteUploader, new()
        {
            for(var u = 0; u < routeUploaders.Length; u++)
            {
                routeUploaders[u] = new U();
                logger.Info("Route uploading using {0}'s uploader running on {1} simultaneous routing task(s)", routeUploaders[u].GetType().Name, simultaneousRoutingTasks);
            }
        }

        public override async Task StartRouting<A,D,U>() //where A: IRoutingAlgorithm, D: IPersonaDownloader, U: IRouteUploader
        {
            if (Initialized == false)
            {
                logger.Info("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                logger.Info("!!   Non-concurrent full-parallel router not initialized   !!");
                logger.Info("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return;
            }

            baseRouterStopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;


            InitializeRoutingAlgorithms<A>();
            InitializeRouteUploaders<U>();
            

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

            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            logger.Info("%  Starting Non-concurrrent full-parallel persona downupload process  %");
            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");

            
            await DownloadPersonasAsync<D>();


            Thread.Sleep(initialDataLoadSleepMilliseconds);

            Stopwatch routingWatch = new Stopwatch();
            routingWatch.Start();
            

            //Task.WaitAll(routingTasks);

            routingWatch.Stop();
            var routingTime = Helper.FormatElapsedTime(routingWatch.Elapsed);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            logger.Info("  Routing time :: {0}", routingTime);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            TotalRoutingTime = routingWatch.Elapsed;


            foreach (Task t in routingTasks)
            {
                Console.WriteLine("Task #{0} status: {1}", t.Id, t.Status);
            }
            

            ComputedRoutesCount = computedRoutes;
            Personas = personas;

            //await UploadRoutesAsync<U>();

            baseRouterStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(baseRouterStopWatch.Elapsed);
            logger.Info("======================================================");
            logger.Info("    Total routing execution time :: {0}", totalTime);
            logger.Info("======================================================");

            TotalExecutionTime = baseRouterStopWatch.Elapsed;
        }

        protected override async Task DownloadPersonasAsync<D>()
        {
            Stopwatch downloadWatch = new Stopwatch();
            downloadWatch.Start();
            
            var downloader = new D();
            downloader.Initialize(_graph,_connectionString,_routeTable);

            logger.Debug("> Thread #{0}\t=>\tDownloadPersonasAsync", Thread.CurrentThread.ManagedThreadId);
            logger.Debug("> DownloadPersonasAsync started");
            
            int[] batchSizes = downloader.GetBatchSizes(regularBatchSize,elementsToProcess);

            int dbRowsProcessed = 0;
            var offset = 0;
            int personasIdx = 0;

            for(var b = 0; b < numberOfBatches; b++)
            {
                var batchSize = batchSizes[b];

                var personasArray = await downloader.DownloadPersonasAsync(_connectionString,_routeTable,batchSize,offset);

                personas.AddRange(personasArray);
                
                personasIdx+=personasArray.Length;//-1;

                dbRowsProcessed += personasArray.Length;

                if (dbRowsProcessed % 5000 == 0)
                {
                    var timeSpan = downloadWatch.Elapsed;
                    var timeSpanMilliseconds = downloadWatch.ElapsedMilliseconds;
                    var result = Helper.DataLoadBenchmark(elementsToProcess, dbRowsProcessed, timeSpan, logger);
                    DBLoadBenchmark(result);
                }

                DispatchData(batchSize, personasIdx);

                offset = offset + batchSize;
            }

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
            logger.Debug("Total number of elements in queues: {0}", elementsToProcess);
            logger.Debug("> DownloadPersonasAsync ended");
        }

        private void DispatchData(int batchSize, int endReference)
        {
            var personasIndex = endReference - batchSize;

            int firstRoutingTaskBatchSize = 0;
            int regularRoutingTaskBatchSize = 0;
            int lastRoutingTaskBatchSize = 0;
            if(simultaneousRoutingTasks > 1)
            {
                var initialRoutingTaskBatchSize = batchSize / simultaneousRoutingTasks;
                firstRoutingTaskBatchSize = (initialRoutingTaskBatchSize * 80 / 100 > 0) ? initialRoutingTaskBatchSize * 80 / 100 : 1;
            }
            
            regularRoutingTaskBatchSize = (simultaneousRoutingTasks - 1 > 0) ? (batchSize - firstRoutingTaskBatchSize) / (simultaneousRoutingTasks - 1) : 0;
            lastRoutingTaskBatchSize = batchSize - firstRoutingTaskBatchSize - regularRoutingTaskBatchSize * (simultaneousRoutingTasks - 2);

            int[] routingTaskBatchSizes = new int[simultaneousRoutingTasks];

            routingTaskBatchSizes[0] = firstRoutingTaskBatchSize;
            for (var s = 1; s < routingTaskBatchSizes.Length-1; s++)
            {
                routingTaskBatchSizes[s] = regularRoutingTaskBatchSize;
            }
            routingTaskBatchSizes[routingTaskBatchSizes.Length-1] = lastRoutingTaskBatchSize;

            for (var t = 0; t < simultaneousRoutingTasks; t++)
            {
                Persona[] personaTaskArray = new Persona[routingTaskBatchSizes[t]];
                for(var p = 0; p < routingTaskBatchSizes[t]; p++)
                {
                    personaTaskArray[p] = personas[personasIndex];
                    personasIndex++;
                }
                ScheduleRoutingTask(personaTaskArray);
                logger.Debug("Task array {0} (of {1}) has been scheduled", t+1, simultaneousRoutingTasks);
            }
        }

        private void ScheduleRoutingTask(Persona[] personaTaskArray)
        {
            for(var t = 0; t < routingTasks.Length; t++)
            {
                var taskIndex = t;
                if(routingTasks[taskIndex].IsCompleted)
                {
                    routingTasks[taskIndex] = Task.Run(() => CalculateRoutes(taskIndex, personaTaskArray));
                    break;
                }
            }
            Task.WaitAny(routingTasks);
        }

        private void DBLoadBenchmark(DataSetBenchmark benchmark)
        {
            dBSetBenchmark.PendingElements = benchmark.PendingElements;
            dBSetBenchmark.ProcessedElements = benchmark.ProcessedElements;
            dBSetBenchmark.ProcessingRate = benchmark.ProcessingRate;
            dBSetBenchmark.ElapsedTime = benchmark.ElapsedTime;
            dBSetBenchmark.ExpectedCompletionTime = benchmark.ExpectedCompletionTime;
        }

        private async void CalculateRoutes(int taskIndex, Persona[] personaTaskArray)
        {
            IRoutingAlgorithm routingAlgorithm = routingAlgorithms[taskIndex];
            IRouteUploader uploader = routeUploaders[taskIndex];

            logger.Debug("> CalculateRoutes started on Thread #{0} with {1} elements", Thread.CurrentThread.ManagedThreadId, personaTaskArray.Length);
            for(var i = 0; i < personaTaskArray.Length; i++)
            {
                var persona = personaTaskArray[i];
                try
                {
                    var routeFound = CalculateRoute(routingAlgorithm, ref persona);
                        
                    if(routeFound)
                    {
                        Interlocked.Increment(ref computedRoutes);
                        persona.SuccessfulRouteComputation = true;
                    }
                }
                catch (Exception e)
                {
                    persona.SuccessfulRouteComputation = false;
                    logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                }
            }
            logger.Debug("> Uploading routes computed on Thread #{0}", Thread.CurrentThread.ManagedThreadId);

            var uploadBatch = personaTaskArray.ToList().FindAll(p => p.SuccessfulRouteComputation == true);
            await UploadRoutesAsync(taskIndex,uploadBatch);

            logger.Debug("> CalculateRoutes ended on Thread #{0}", Thread.CurrentThread.ManagedThreadId);
        }

        protected override async Task UploadRoutesAsync(int taskIndex, List<Persona> personaBatch)// where U: IRouteUploader
        {
            Stopwatch uploadWatch = new Stopwatch();
            uploadWatch.Start();

            IRouteUploader uploader = routeUploaders[taskIndex];

            await uploader.UploadRoutesAsync(_connectionString,_routeTable,personaBatch,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

            uploadWatch.Stop();
            var downloadTime = Helper.FormatElapsedTime(uploadWatch.Elapsed);
            logger.Info("UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU");
            logger.Info("  Persona upload time :: {0}", downloadTime);
            logger.Info("UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU");
            TotalUploadingTime = uploadWatch.Elapsed;
            
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
        }

        protected override async Task UploadRoutesAsync<U>()
        {
            await Task.CompletedTask;
        }
    }
}
