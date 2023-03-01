using NLog;
using System.Diagnostics;
using Npgsql;
using SytyRouting.Model;
using NetTopologySuite.Geometries;
using SytyRouting.Algorithms;
using System.Collections.Concurrent;
using SytyRouting.DataBase;

namespace SytyRouting.Routing
{
    public class RouterBatchUpload : BaseRouter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        //public List<Persona> Personas {private set; get;} = null!;

        private List<Persona> personas = new List<Persona>();
        
        //private Graph _graph;
        // private string _routeTable;
        // private string _auxiliaryTable;

        private static int simultaneousRoutingTasks = Environment.ProcessorCount;

        private Task[] routingTasks = new Task[simultaneousRoutingTasks];

        private ConcurrentQueue<Persona[]> personaTaskArraysQueue = new ConcurrentQueue<Persona[]>();

        private int taskArraysQueueThreshold = simultaneousRoutingTasks;

        private int elementsToProcess = 0;
        private int processedDbElements = 0;
//        public int ComputedRoutes {private set; get;} = 0;
        private static int computedRoutes = 0;

        private static int uploadedRoutes = 0;

        private bool routingTasksHaveEnded = false;
    
        private int regularBatchSize = simultaneousRoutingTasks * Configuration.RegularRoutingTaskBatchSize;

        private Stopwatch stopWatch = new Stopwatch();

        private int originEqualsDestinationErrors = 0;

        // public RouterOneTimeAllUpload(Graph graph, string routeTable) : base(graph,routeTable)
        // {
        //     _graph = graph;
        //     _routeTable = routeTable;
        //     _auxiliaryTable = routeTable+Configuration.AuxiliaryTableSuffix;
        // }

        public override async Task StartRouting<A,U>() //where A: IRoutingAlgorithm, new()
        {
            stopWatch.Start();

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

            Task.WaitAll(routingTasks);
            Task.WaitAll(downloadTask); //debug <-
            routingTasksHaveEnded = true;
            //var uploader = new U();
            //await uploader.PropagateResultsAsync(Configuration.ConnectionString,_auxiliaryTable,_routeTable);
            await SeveralRoutesUploader.PropagateResultsAsync(Configuration.ConnectionString,_auxiliaryTable,_routeTable);
            Task.WaitAll(monitorTask);

            ComputedRoutesCount = computedRoutes;
            Personas = personas;

            //await UploadRoutesAsync();

            stopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(stopWatch.Elapsed);
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

        // protected override void CalculateRoutes<A>(int taskIndex) //where A: IRoutingAlgorithm, new()
        // {
        // }

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
                        var homeX = persona.HomeLocation!.X;
                        var homeY = persona.HomeLocation.Y;
                        
                        var workX = persona.WorkLocation!.X;
                        var workY = persona.WorkLocation.Y;
                        
                        var requestedTransportModes = persona.RequestedTransportSequence;

                        TimeSpan initialTime = TimeSpan.Zero;

                        List<Node> route = null!;

                        var origin = _graph.GetNodeByLongitudeLatitude(persona.HomeLocation!.X, persona.HomeLocation.Y, isSource: true);
                        var destination = _graph.GetNodeByLongitudeLatitude(persona.WorkLocation!.X, persona.WorkLocation.Y, isTarget: true);

                        if(origin == destination)
                        {
                            logger.Debug("Origin and destination nodes are equal for Persona Id {0}", persona.Id);

                            persona.Route = routingAlgorithm.TwoPointLineString(homeX, homeY, workX, workY, TransportModes.DefaultMode, initialTime);

                            if(persona.Route.IsEmpty)
                            {
                                logger.Debug("Route is empty for Persona Id {0} !!!!", persona.Id);
                                originEqualsDestinationErrors++;
                                continue;
                            }

                            persona.TTextTransitions = routingAlgorithm.SingleTransportModeTransition(persona, origin, destination, TransportModes.DefaultMode);

                            persona.SuccessfulRouteComputation = true;

                            Interlocked.Increment(ref computedRoutes);
                        }
                        else
                        {
                            route = routingAlgorithm.GetRoute(origin, destination, requestedTransportModes);
                        }

                        if(route != null)
                        {
                            if(route.Count > 0)
                            {
                                persona.Route = routingAlgorithm.NodeRouteToLineStringMSeconds(homeX, homeY, workX, workY, route, initialTime, persona.StartDateTime);

                                persona.TTextTransitions = routingAlgorithm.GetTransportModeTransitions();

                                persona.SuccessfulRouteComputation = true;

                                Interlocked.Increment(ref computedRoutes);
                            }
                            else
                            {
                                logger.Debug("Route is empty for Persona Id {0}", persona.Id);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        persona.SuccessfulRouteComputation = false;
                        logger.Debug(" ==>> Unable to compute route: Persona Id {0}: {1}", persona.Id, e);
                    }
                }
                //debug:
                try
                {
                    Task uploadTask = Task.Run(() => UploadRoutesAsync2<U>(personaArray));
                    Task.WaitAll(uploadTask);
                }
                catch(Exception e)
                {
                    logger.Debug("Error uploading routes by batches: {0}",e.Message);
                }
                //
            }
        }

        private void MonitorRouteCalculation()
        {
            int monitorSleepMilliseconds = Configuration.MonitorSleepMilliseconds; // 5_000;
            while(true)
            {
                var timeSpan = stopWatch.Elapsed;
                var timeSpanMilliseconds = stopWatch.ElapsedMilliseconds;
                Helper.DataLoadBenchmark(elementsToProcess, computedRoutes, timeSpan, timeSpanMilliseconds, logger);
                logger.Info("DB elements already processed: {0} ({1:0.000} %). Computed routes: {2} ({3:0.000} %)", processedDbElements, (double)processedDbElements / elementsToProcess * 100, computedRoutes, (double)computedRoutes / elementsToProcess * 100);
                logger.Info("");

                if(routingTasksHaveEnded)
                {
                    if(processedDbElements != elementsToProcess)
                    {
                        logger.Info(" ==>> Inconsistent number of processed elements.");
                    }
                    logger.Debug("{0} routes (out of {1}) uploaded ({2} %)", uploadedRoutes, personas.Count, 100 * uploadedRoutes / personas.Count);
                    return;
                }

                Thread.Sleep(monitorSleepMilliseconds);
            }
        }

        private int[] GetBatchPartition(int regularSlice, int whole, int numberOfSlices)
        {
            int lastSlice = whole - regularSlice * (numberOfSlices - 1);
            int[] batchPartition = new int[numberOfSlices];
            for (var i = 0; i < batchPartition.Length-1; i++)
            {
                batchPartition[i] = regularSlice;
            }
            batchPartition[batchPartition.Length-1] = lastSlice;

            return batchPartition;
        }

        // protected override async Task UploadRoutesAsync<U>()
        // {
        //     Stopwatch uploadStopWatch = new Stopwatch();
        //     uploadStopWatch.Start();

        //     // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
        //     var connectionString = Configuration.ConnectionString;       

        //     var auxiliaryTable = _auxiliaryTable;
        //     var routeTable = _routeTable;

        //     //var uploader = new DataBase.SeveralRoutesUpload();
        //     var uploader = new U();

        //     int uploadFails = await uploader.UploadRoutesAsync(connectionString,auxiliaryTable,routeTable,personas);

        //     uploadStopWatch.Stop();
        //     var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
        //     logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);
        //     logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", personas.Count - uploadFails, auxiliaryTable, totalTime);
        //     logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personas.Count, 100.0 * (double)uploadFails / (double)personas.Count);
        //     logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
        //     logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)personas.Count);
        // }

        private async Task UploadRoutesAsync2<U>(Persona[] personaArray) where U: IRouteUploader, new()
        {
            Stopwatch uploadStopWatch = new Stopwatch();
            uploadStopWatch.Start();

            // var connectionString = Configuration.LocalConnectionString;  // Local DB for testing
            var connectionString = Configuration.ConnectionString;       

            // var auxiliaryTable = _auxiliaryTable;
            // var routeTable = _routeTable;

            var uploader = new U();

            int uploadFails = await uploader.UploadRoutesAsync(connectionString,_auxiliaryTable,_routeTable,personaArray.ToList());

            uploadStopWatch.Stop();
            var totalTime = Helper.FormatElapsedTime(uploadStopWatch.Elapsed);
            logger.Debug("Transport sequence validation errors: {0} ({1} % of the requested transport sequences were overridden)", sequenceValidationErrors, 100.0 * (double)sequenceValidationErrors / (double)personas.Count);
            logger.Info("{0} Routes successfully uploaded to the database ({1}) in {2} (d.hh:mm:s.ms)", personaArray.Length - uploadFails, _auxiliaryTable, totalTime);
            logger.Debug("{0} routes (out of {1}) failed to upload ({2} %)", uploadFails, personaArray.Length, 100.0 * (double)uploadFails / (double)personaArray.Length);
            logger.Debug("'Origin = Destination' errors: {0} ({1} %)", originEqualsDestinationErrors, 100.0 * (double)originEqualsDestinationErrors / (double)personas.Count);
            logger.Debug("                 Other errors: {0} ({1} %)", uploadFails - originEqualsDestinationErrors, 100.0 * (double)(uploadFails - originEqualsDestinationErrors) / (double)personas.Count);
        }
    }
}
