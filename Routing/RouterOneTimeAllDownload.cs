using NLog;
using System.Diagnostics;
using SytyRouting.Model;

namespace SytyRouting.Routing
{
    public class RouterOneTimeAllDownload : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        

        public override async Task StartRouting<A,D,U>() //where A: IRoutingAlgorithm, D: IPersonaDownloader, U: IRouteUploader
        {
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

            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            logger.Info(":  Starting One-Time-All persona dowload process.  :");
            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");

            
            Task downloadTask = Task.Run(() => DownloadPersonasAsync<D>());
            Task.WaitAll(downloadTask);


            Thread.Sleep(initialDataLoadSleepMilliseconds); // <- This line is only to match added sleep time in other download strategies
            if(personaTaskArraysQueue.Count < simultaneousRoutingTasks)
            {
                logger.Info(" ==>> Initial DB load timeout ({0} ms) elapsed. Unable to start the routing process.", initialDataLoadSleepMilliseconds);
                return;
            }
            
            
            Stopwatch routingWatch = new Stopwatch();
            routingWatch.Start();

            for(int taskIndex = 0; taskIndex < routingTasks.Length; taskIndex++)
            {
                int t = taskIndex;
                routingTasks[t] = Task.Run(() => CalculateRoutes<A,U>(t));
            }
            Task monitorTask = Task.Run(() => MonitorRouteCalculation());

            Task.WaitAll(routingTasks);
            routingWatch.Stop();
            var routingTime = Helper.FormatElapsedTime(routingWatch.Elapsed);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            logger.Info("  Routing time :: {0}", routingTime);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            TotalRoutingTime = routingWatch.Elapsed;

            routingTasksHaveEnded = true;
            
            Task.WaitAll(monitorTask);

            ComputedRoutesCount = computedRoutes;
            Personas = personas;

            await UploadRoutesAsync<U>();

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

            int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

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

            downloadWatch.Stop();
            var downloadTime = Helper.FormatElapsedTime(downloadWatch.Elapsed);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
            logger.Info("  Persona download time :: {0}", downloadTime);
            logger.Info("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
            TotalDownloadingTime = downloadWatch.Elapsed;
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
    }
}
