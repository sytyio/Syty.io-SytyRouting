using System.Diagnostics;
using NLog;
using SytyRouting.Algorithms;
using SytyRouting.Routing;

namespace SytyRouting.DataBase
{
    public class RouteUploadBenchmarking : BaseBenchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static async Task Start(Graph graph)
        {
            _graph = graph;

            int numberOfRows = 200;//60; //1360;
            var connectionString = Configuration.ConnectionString;
            var personaRouteTable = new DataBase.PersonaRouteTable(connectionString);
                        
                        
            string baseRouteTable = Configuration.PersonaRouteTable;


            //////////////
            // /////////////  ////////////// //
            uploadStrategies.Add("On-Time All, single DB connection, COPY, TEMP AUX (ref.)");
            var routeTable = baseRouteTable + "_t70";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            var comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            var totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterOneTimeAllUpload>(graph,connectionString,routeTable,comparisonTable);
            totalTimes.Add(totalTime);
            
            var comparisonTable70 = comparisonTable;
            compTableNames.Add(comparisonTable);

            var comparisonResult = "Reference";
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////



            // //////////////
            // // /////////////  ////////////// //
            uploadStrategies.Add("On-Time All, single DB connection, INSERT BATCHED");
            routeTable = baseRouteTable + "_t78";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderINSERTBATCHED,
                                    Routing.RouterOneTimeAllUpload>(graph,connectionString,routeTable,comparisonTable);
            totalTimes.Add(totalTime);

            var comparisonTable78 = comparisonTable;
            compTableNames.Add(comparisonTable);

            comparisonResult = await DataBase.RouteUploadBenchmarking.CompareUploadedRoutesAsync(comparisonTable70,comparisonTable78);
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////



            // ///////////////
            // // /////////////  ////////////// //
            uploadStrategies.Add("As computed (batch), single DB connection");
            routeTable = baseRouteTable + "_t75";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterBatchUpload>(graph,connectionString,routeTable,comparisonTable);
            totalTimes.Add(totalTime);

            var comparisonTable75 = comparisonTable;
            compTableNames.Add(comparisonTable);

            comparisonResult = await DataBase.RouteUploadBenchmarking.CompareUploadedRoutesAsync(comparisonTable70,comparisonTable75);
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////

            



            var uploadStrategiesArray =  uploadStrategies.ToArray();
            var tableNamesArray = tableNames.ToArray();
            var totalTimesArray = totalTimes.ToArray();
            var routingTimesArray = routingTimes.ToArray();
            var uploadingTimesArray = uploadingTimes.ToArray();
            var uploadResultsArray = uploadResults.ToArray();
            var comparisonResultsArray = comparisonResults.ToArray();

            logger.Info("=======================================================================================================================================================================================================================================================================================");
            logger.Info("{0} Routes Benchmarking",numberOfRows);
            logger.Info("=======================================================================================================================================================================================================================================================================================");
            logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}","Strategy","Table"," Routing Time","Uploading Time","Uploading-Routing Ratio","   Total Time","Processing Rate","Uploading Test","Comparison Test");
            logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}","        ","     ","d.hh:mm:ss.ms "," d.hh:mm:ss.ms ","                      %","d.hh:mm:ss.ms ","      (items/s)","              ","               ");
            logger.Info("=======================================================================================================================================================================================================================================================================================");
            for(int i=0; i<comparisonResultsArray.Length; i++)
            {
                try
                {
                    double processingRate=-1.0;
                    double uploadingRoutingRatio = -1.0;
                    if(uploadResultsArray[i].Equals("SUCCEEDED"))
                    {
                        processingRate = Helper.GetProcessingRate(numberOfRows,totalTimesArray[i].TotalMilliseconds);
                        uploadingRoutingRatio = 100.0 * uploadingTimesArray[i].TotalSeconds / routingTimesArray[i].TotalSeconds;
                    }
                    
                    logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}",
                                                                uploadStrategiesArray[i],
                                                                tableNamesArray[i],
                                                                Helper.FormatElapsedTime(routingTimesArray[i]),
                                                                Helper.FormatElapsedTime(uploadingTimesArray[i]),
                                                                uploadingRoutingRatio,
                                                                Helper.FormatElapsedTime(totalTimesArray[i]),
                                                                processingRate,
                                                                uploadResultsArray[i],
                                                                comparisonResultsArray[i]);
                }
                catch (Exception e)
                {
                    logger.Debug("Error displying data: {0}", e.Message);
                }
            }
            logger.Info("=======================================================================================================================================================================================================================================================================================");


            await CleanComparisonTablesAsync(Configuration.ConnectionString,compTableNames);

        }

        private static async Task<TimeSpan> Run<A,D,U,R>(Graph graph, string connectionString, string routeTable, string comparisonTable) where A: IRoutingAlgorithm, new() where D: IPersonaDownloader, new() where U: IRouteUploader, new() where R: IRouter, new()
        {
            Stopwatch benchmarkStopWatch = new Stopwatch();
            benchmarkStopWatch.Start();

            //var uploader = new U();
            var router = new R();

            router.Initialize(_graph, connectionString, routeTable, comparisonTable: comparisonTable);
            await router.StartRouting<A,D,U>();

            var personas = router.GetPersonas();
            var computedRoutes = router.GetComputedRoutesCount();
            var routingTime = router.GetRoutingTime();
            var uploadingTime = router.GetUploadingTime();
            routingTimes.Add(routingTime);
            uploadingTimes.Add(uploadingTime);

            var uploadTest = await CheckUploadedRoutesAsync(personas, comparisonTable, computedRoutes);
            uploadResults.Add(uploadTest);

            
            benchmarkStopWatch.Stop();
            var executionTime = benchmarkStopWatch.Elapsed;
            var totalTime = Helper.FormatElapsedTime(benchmarkStopWatch.Elapsed);
            logger.Info("---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            logger.Info("Benchmark performed in {0} (HH:MM:S.mS) for the uploader '{1}' and the router '{2}' using the '{3}' algorithm", totalTime, typeof(U).Name, router.GetType().Name, typeof(A).Name);
            logger.Info("---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");

            return executionTime;
        }
    }
}