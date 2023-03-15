using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;
using SytyRouting.DataBase;

namespace SytyRouting.Routing
{
    public class RouterTwoDBConnectionsBatchUpload : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        private ConcurrentQueue<Persona> routesQueue = new ConcurrentQueue<Persona>();
        private int uploadBatchSize = 0;


        public override async Task StartRouting<A,U>() //where A: IRoutingAlgorithm, new()
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
            
            Task downloadTask = Task.Run(() => DownloadPersonaDataAsync());
            
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

            Task uploadTask = Task.Run(() => UploadRoutesAsync3<U>());

            Task.WaitAll(downloadTask); //debug <-
            Task.WaitAll(routingTasks);
            routingTasksHaveEnded = true;
            Task.WaitAll(uploadTask); //debug <-
            Task.WaitAll(monitorTask);

            ComputedRoutesCount = computedRoutes;
            Personas = personas;

            //await UploadRoutesAsync();

            baseRouterStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(baseRouterStopWatch.Elapsed);
            logger.Info("=================================================");
            logger.Info("    Routing execution time :: {0}", totalTime);
            logger.Info("=================================================");
        }

        private async Task DownloadPersonaDataAsync()
        {
            int dBPersonaLoadAsyncSleepMilliseconds = Configuration.DBPersonaLoadAsyncSleepMilliseconds; // 100;

            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var personaTable = _routeTable;

            var batchSize = (regularBatchSize > elementsToProcess) ? elementsToProcess : regularBatchSize;
            var numberOfBatches = (elementsToProcess / batchSize > 0) ? elementsToProcess / batchSize : 1;
            int[] batchSizes = GetBatchPartition(batchSize, elementsToProcess, numberOfBatches);

            //uploadBatchSize = batchSize;
            uploadBatchSize = elementsToProcess;

            int offset = 0;
            for(var batchNumber = 0; batchNumber < numberOfBatches; batchNumber++)
            {
                var currentBatchSize = batchSizes[batchNumber];

                var routingTaskBatchSize = (currentBatchSize / simultaneousRoutingTasks > 0) ? currentBatchSize / simultaneousRoutingTasks : 1;
                int[] routingTaskBatchSizes = GetBatchPartition(routingTaskBatchSize, currentBatchSize, simultaneousRoutingTasks);

                var taskIndex = 0;
                var personaTaskArray = new Persona[routingTaskBatchSizes[taskIndex]];
                var personaIndex = 0;

                // Read location data from 'persona' and create the corresponding latitude-longitude coordinates
                //                        0   1              2              3           4
                var queryString = "SELECT id, home_location, work_location, start_time, requested_transport_modes FROM " + personaTable + " ORDER BY id ASC LIMIT " + currentBatchSize + " OFFSET " + offset;

                await using (var command = new NpgsqlCommand(queryString, connection))
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        var id = Convert.ToInt32(reader.GetValue(0)); // id (int)
                        var homeLocation = (Point)reader.GetValue(1); // home_location (Point)
                        var workLocation = (Point)reader.GetValue(2); // work_location (Point)
                        var startTime = (DateTime)reader.GetValue(3); // start_time (TIMESTAMPTZ)
                        var requestedSequence = reader.GetValue(4); // transport_sequence (text[])
                        byte[] requestedTransportSequence;
                        if(requestedSequence is not null && requestedSequence != DBNull.Value)
                        {
                             requestedTransportSequence = ValidateTransportSequence(id, homeLocation, workLocation, (string[])requestedSequence);
                        }
                        else
                        {
                            requestedTransportSequence = new byte[0];
                        }

                        var persona = new Persona {Id = id, HomeLocation = homeLocation, WorkLocation = workLocation, StartDateTime = startTime, RequestedTransportSequence = requestedTransportSequence};
                        
                        personas.Add(persona);
                        
                        personaTaskArray[personaIndex] = persona;
                        personaIndex++;

                        if(personaIndex >= routingTaskBatchSizes[taskIndex])
                        {
                            personaTaskArraysQueue.Enqueue(personaTaskArray);
                            personaIndex = 0;
                            taskIndex++;
                            if(taskIndex < simultaneousRoutingTasks)
                                personaTaskArray = new Persona[routingTaskBatchSizes[taskIndex]];
                        }
                        processedDbElements++;
                    }
                }
                offset += currentBatchSize;

                while(personaTaskArraysQueue.Count > taskArraysQueueThreshold)
                    Thread.Sleep(dBPersonaLoadAsyncSleepMilliseconds);
            }
            await connection.CloseAsync();
        }

        protected override void CalculateRoutes<A,U>(int taskIndex) //where A: IRoutingAlgorithm, new()
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

        private async Task UploadRoutesAsync3<U>() where U: IRouteUploader, new()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;       

            var uploader = new U();

            List<Persona> personasUploadBatch = new List<Persona>(uploadBatchSize);
            int uploadFails = 0;
            int uploadedRoutes = 0;

            //debug:
            List<Persona> uploadedRoutesList = new List<Persona>(elementsToProcess);
            //

            //debug:
            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
            while(true)
            {

                logger.Debug("{0} elements in the uploading queue",routesQueue.Count);
                if(routesQueue.Count>=uploadBatchSize)
                {
                    int count = 0;
                    while(routesQueue.TryDequeue(out Persona? persona) && count<uploadBatchSize)
                    {
                        if(persona!=null)
                        {
                            //debug:
                            if(persona.Id==282)
                            {
                                Console.WriteLine("Problem here");
                            }
                            uploadedRoutesList.Add(persona);
                            //
                            personasUploadBatch.Add(persona);
                            count++;
                        }
                        else
                        {
                            Console.WriteLine("Null person");
                        }
                    }
                    logger.Debug("Uploading {0} routes",personasUploadBatch.Count);
                    
                    //uploadFails = await uploader.UploadRoutesAsync(connectionString,_auxiliaryTable,_routeTable,personasUploadBatch);
                    uploadFails = await uploader.UploadRoutesAsync(connectionString,_routeTable,personasUploadBatch);

                    uploadedRoutes += count - uploadFails;
                    logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                    personasUploadBatch.Clear();
                    uploadedRoutes = 0;
                }

                if(routingTasksHaveEnded)
                {
                    var remainingRoutes = routesQueue.ToList();

                    logger.Debug("Routing tasks have ended. Uploading {0} remaining routes",remainingRoutes.Count);
                    
                    //uploadFails = await uploader.UploadRoutesAsync(connectionString,_auxiliaryTable,_routeTable,remainingRoutes);
                    uploadFails = await uploader.UploadRoutesAsync(connectionString,_routeTable,remainingRoutes);

                    //uploadFails += await SeveralRoutesUploader.PropagateResultsAsync(connectionString,_auxiliaryTable,_routeTable);

                    uploadedRoutes += remainingRoutes.Count - uploadFails;
                    logger.Debug("{0} routes uploaded in total ({1} upload fails)",uploadedRoutes,uploadFails);
                
                    uploadStopWatch.Stop();
                    var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
                    logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personasUploadBatch.Count);
                    logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", uploadedRoutes, _auxiliaryTable, totalTime);
                    logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personasUploadBatch.Count, 100.0 * (double)uploadFails / (double)personasUploadBatch.Count);
                    logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personasUploadBatch.Count);
                    logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)personasUploadBatch.Count);
             
                    return;
                }

                

                Thread.Sleep(monitorSleepMilliseconds);
            }
            //
        }
    }
}
