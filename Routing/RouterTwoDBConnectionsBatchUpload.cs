using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;

namespace SytyRouting.Routing
{
    public class RouterTwoDBConnectionsBatchUpload : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private ConcurrentQueue<Persona> routesQueue = new ConcurrentQueue<Persona>();

        public override async Task StartRouting<A,D,U>() //where A: IRoutingAlgorithm, D: IPersonaDownloader, U: IRouteUploader
        {
            baseRouterStopWatch.Start();

            int initialDataLoadSleepMilliseconds = Configuration.InitialDataLoadSleepMilliseconds; // 2_000;

            elementsToProcess = await Helper.DbTableRowCount(_routeTable, logger);
            //elementsToProcess = 6; // 500_000; // 1357; // 13579;                         // For testing with a reduced number of 'personas'
            //elementsToProcess = await Helper.DbTableRowCount(Configuration.RoutingBenchmarkTable, logger);

            if(elementsToProcess < 1)
            {
                logger.Info("No DB elements to process");
                return;
            }
            else if(elementsToProcess < simultaneousRoutingTasks)
            {
                simultaneousRoutingTasks = elementsToProcess;
            }
            
            Task downloadTask = Task.Run(() => DownloadPersonasAsync<D>());
            
            Thread.Sleep(initialDataLoadSleepMilliseconds);
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

            Task uploadTask = Task.Run(() => UploadRoutesAsync<U>());

            Task.WaitAll(downloadTask); //debug <-
            Task.WaitAll(routingTasks);

            TotalRoutingTime = baseRouterStopWatch.Elapsed;

            routingTasksHaveEnded = true;
            
            Task.WaitAll(uploadTask); //debug <-
            Task.WaitAll(monitorTask);

            ComputedRoutesCount = computedRoutes;
            Personas = personas;


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
                    
                    await uploader.UploadRoutesAsync(_connectionString,_routeTable,uploadBatch);

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
                    
                    await uploader.UploadRoutesAsync(_connectionString,_routeTable,remainingRoutes);

                    uploadedRoutes += remainingRoutes.Count - uploadFails;
                    logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                
                    uploadStopWatch.Stop();
                    TotalUploadingTime = uploadStopWatch.Elapsed;
                    var totalTime = Helper.FormatElapsedTime(TotalUploadingTime);
                    logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", uploadedRoutes, _auxiliaryTable, totalTime);
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
