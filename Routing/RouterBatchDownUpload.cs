using NLog;
using System.Collections.Concurrent;
using System.Diagnostics;
using SytyRouting.Model;

namespace SytyRouting.Routing
{
    public class RouterBatchDownUpload : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private ConcurrentQueue<Persona> routesQueue = new ConcurrentQueue<Persona>();

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
            logger.Info("%     Starting Batched downroutupload process      %");
            logger.Info("%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
            
            Task downloadTask = Task.Run(() => DownloadPersonasAsync<D>());

            Thread.Sleep(initialDataLoadSleepMilliseconds);
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
            Task uploadTask = Task.Run(() => UploadRoutesAsync<U>());


            Task.WaitAll(downloadTask);
            Task.WaitAll(routingTasks);

            routingWatch.Stop();
            var routingTime = Helper.FormatElapsedTime(routingWatch.Elapsed);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            logger.Info("  Routing time :: {0}", routingTime);
            logger.Info("RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR");
            TotalRoutingTime = routingWatch.Elapsed;
            routingTasksHaveEnded = true;

            Task.WaitAll(uploadTask);
            Task.WaitAll(monitorTask);

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

                while(personaTaskArraysQueue.Count > taskArraysQueueThreshold)
                    Thread.Sleep(dBPersonaLoadAsyncSleepMilliseconds);
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
                            routesQueue.Enqueue(persona);
                            Interlocked.Increment(ref computedRoutes);
                            persona.SuccessfulRouteComputation = true;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                    }
                }
            }
        }

        protected override async Task UploadRoutesAsync<U>()// where U: IRouteUploader, new()
        {
            Stopwatch uploadStopWatch = new Stopwatch();

            var uploader = new U();

            var uploadBatchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;

            List<Persona> uploadBatch = new List<Persona>(uploadBatchSize);
            int uploadFails = 0;
            int uploadedRoutes = 0;

            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
            while(true)
            {
                logger.Debug("{0} elements in the uploading queue",routesQueue.Count);
                if(routesQueue.Count>=uploadBatchSize)
                {
                    uploadStopWatch.Start();
                    while(uploadBatch.Count<=uploadBatchSize && routesQueue.TryDequeue(out Persona? persona))
                    {
                        if(persona!=null)
                        {
                            uploadBatch.Add(persona);
                        }
                    }
                    logger.Debug("Uploading {0} routes",uploadBatch.Count);
                    
                    await uploader.UploadRoutesAsync(_connectionString,_routeTable,uploadBatch,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

                    uploadedRoutes += uploadBatch.Count - uploadFails;
                    logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                    uploadBatch.Clear();
                    uploadStopWatch.Stop();
                }

                if(routingTasksHaveEnded)
                {
                    uploadStopWatch.Start();

                    var remainingRoutes = routesQueue.ToList();

                    logger.Debug("Routing tasks have ended. Computed routes queue dump. Uploading {0} remaining routes",remainingRoutes.Count);
                    
                    await uploader.UploadRoutesAsync(_connectionString,_routeTable,remainingRoutes,comparisonTable:_comparisonTable,benchmarkingTable:_benchmarkTable);

                    uploadedRoutes += remainingRoutes.Count - uploadFails;
                    logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                
                    uploadStopWatch.Stop();
                    TotalUploadingTime = uploadStopWatch.Elapsed;
                    var totalTime = Helper.FormatElapsedTime(TotalUploadingTime);
                    logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", uploadedRoutes, _comparisonTable, totalTime);
                    logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, uploadBatch.Count, 100.0 * (double)uploadFails / (double)uploadBatch.Count);
                    logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)uploadBatch.Count);
                    logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)uploadBatch.Count);
             
                    return;
                }

                Thread.Sleep(monitorSleepMilliseconds);
            }
        }
    }
}