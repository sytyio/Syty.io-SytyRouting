using System.Diagnostics;
using NetTopologySuite.Geometries;
using NetTopologySuite.Utilities;
using NLog;
using Npgsql;
using SytyRouting.Algorithms;
using SytyRouting.Model;
using SytyRouting.Routing;

namespace SytyRouting.DataBase
{
    public class PersonaDownloadBenchmarking : BaseBenchmarking
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task Start(Graph graph, int numberOfRuns)
        {
            if (numberOfRuns < 1)
            {
                logger.Debug("Only serious requests, please. Number of runs should be greater or equal to 1");
                return;
            }

            _graph = graph;

            int numberOfRows = 200; //1360;//60; //1360;
            var connectionString = Configuration.ConnectionString;
            var personaRouteTable = new DataBase.PersonaRouteTable(connectionString);
                        
                        
            string baseRouteTable = Configuration.PersonaRouteTable;

            //////////////
            // /////////////  ////////////// //
            downloadStrategies.Add("Sequential dowload->routing->upload; single DB conn., COPY, TEMP AUX tab. (ref.)");
            var routeTable = baseRouteTable + "_t47d";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            var comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            var totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterOneTimeAllDownload>(graph,connectionString,routeTable,comparisonTable,numberOfRuns);

            totalTime = TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / numberOfRuns);

            totalTimes.Add(totalTime);
            
            var comparisonTable47 = comparisonTable;
            compTableNames.Add(comparisonTable);

            var comparisonResult = "Reference";
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////



            //////////////
            // /////////////  ////////////// //
            downloadStrategies.Add("Full-Parallel download-routing, dual DB connection, COPY, TEMP AUX tab.");
            routeTable = baseRouteTable + "_t48d";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterFullParallelDownload>(graph,connectionString,routeTable,comparisonTable,numberOfRuns);

            totalTime = TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / numberOfRuns);

            totalTimes.Add(totalTime);
            
            var comparisonTable48 = comparisonTable;
            compTableNames.Add(comparisonTable);

            comparisonResult = await DataBase.RouteUploadBenchmarking.CompareUploadedRoutesAsync(comparisonTable47,comparisonTable48);
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////



            //////////////
            // /////////////  ////////////// //
            downloadStrategies.Add("Full-parall. downl., routing, no concurrent, dual DB conn., COPY, TEMP AUX tab.");
            routeTable = baseRouteTable + "_t56d";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterFullParallelDownloadNonConcurrent>(graph,connectionString,routeTable,comparisonTable,numberOfRuns);

            totalTime = TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / numberOfRuns);

            totalTimes.Add(totalTime);
            
            var comparisonTable56 = comparisonTable;
            compTableNames.Add(comparisonTable);

            comparisonResult = await DataBase.RouteUploadBenchmarking.CompareUploadedRoutesAsync(comparisonTable47,comparisonTable56);
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////



            //////////////
            // /////////////  ////////////// //
            downloadStrategies.Add("Parall. routing, batch downl., sequ. upload; single DB conn., COPY, TEMP AUX tab.");
            routeTable = baseRouteTable + "_t57d";
            await personaRouteTable.CreateDataSet(Configuration.PersonaTable,routeTable,numberOfRows);
            comparisonTable = routeTable+Configuration.AuxiliaryTableSuffix+"_comp";
            tableNames.Add(routeTable);

            totalTime = await Run<Algorithms.Dijkstra.Dijkstra,
                                    DataBase.PersonaDownloaderArrayBatch,
                                    DataBase.RouteUploaderCOPY,
                                    Routing.RouterBatchDownload>(graph,connectionString,routeTable,comparisonTable,numberOfRuns);

            totalTime = TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / numberOfRuns);

            totalTimes.Add(totalTime);
            
            var comparisonTable57 = comparisonTable;
            compTableNames.Add(comparisonTable);

            comparisonResult = await DataBase.RouteUploadBenchmarking.CompareUploadedRoutesAsync(comparisonTable47,comparisonTable57);
            comparisonResults.Add(comparisonResult);
            // //////////////
            // //////////////


            


            var uploadStrategiesArray =  downloadStrategies.ToArray();
            var tableNamesArray = tableNames.ToArray();
            var totalTimesArray = totalTimes.ToArray();
            var routingTimesArray = routingTimes.ToArray();
            var uploadingTimesArray = uploadingTimes.ToArray();
            var downloadingTimesArray = downloadingTimes.ToArray();
            var uploadResultsArray = uploadResults.ToArray();
            var comparisonResultsArray = comparisonResults.ToArray();

            logger.Info("=======================================================================================================================================================================================================================================================================================================");
            logger.Info("{0} Personas Download Benchmarking. {1} runs",numberOfRows,numberOfRuns);
            logger.Info("=======================================================================================================================================================================================================================================================================================================");
            logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}\t{9,20}","Strategy","Table"," Routing Time","Downloading Time","Uploading Time","Download-Routing Ratio","   Total Time","Processing Rate","Uploading Test","Comparison Test");
            logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}\t{9,20}","        ","     ","d.hh:mm:ss.ms "," d.hh:mm:ss.ms "," d.hh:mm:ss.ms ","                      %","d.hh:mm:ss.ms ","      (items/s)","              ","               ");
            logger.Info("=======================================================================================================================================================================================================================================================================================================");
            for(int i=0; i<comparisonResultsArray.Length; i++)
            {
                double processingRate=-1.0;
                double downloadingRoutingRatio = -1.0;
                if(uploadResultsArray[i].Equals("SUCCEEDED"))
                {
                    processingRate = Helper.GetProcessingRate(numberOfRows,totalTimesArray[i].TotalMilliseconds);
                    downloadingRoutingRatio = 100.0 * downloadingTimesArray[i].TotalSeconds / routingTimesArray[i].TotalSeconds;
                }
                
                logger.Info("{0,80}\t{1,20}\t{2,20}\t{3,20}\t{4,20}\t{5,20}\t{6,20}\t{7,20}\t{8,20}\t{9,20}",
                                                            uploadStrategiesArray[i],
                                                            tableNamesArray[i],
                                                            Helper.FormatElapsedTime(routingTimesArray[i]),
                                                            Helper.FormatElapsedTime(downloadingTimesArray[i]),
                                                            Helper.FormatElapsedTime(uploadingTimesArray[i]),
                                                            downloadingRoutingRatio,
                                                            Helper.FormatElapsedTime(totalTimesArray[i]),
                                                            processingRate,
                                                            uploadResultsArray[i],
                                                            comparisonResultsArray[i]);
            }
            logger.Info("=======================================================================================================================================================================================================================================================================================================");


            await CleanComparisonTablesAsync(Configuration.ConnectionString,compTableNames);

        }

        public static async Task<TimeSpan> Run<A,D,U,R>(Graph graph, string connectionString, string routeTable, string comparisonTable, int numberOfRuns) where A: IRoutingAlgorithm, new() where D: IPersonaDownloader, new() where U: IRouteUploader, new() where R: IRouter, new()
        {
            Stopwatch benchmarkStopWatch = new Stopwatch();
            benchmarkStopWatch.Start();

            //var uploader = new U();
            var router = new R();
            router.Initialize(_graph, connectionString, routeTable, comparisonTable: comparisonTable);

            TimeSpan routingTime = TimeSpan.Zero;
            TimeSpan uploadingTime = TimeSpan.Zero;
            TimeSpan downloadingTime = TimeSpan.Zero;

            numberOfRuns = numberOfRuns > 0 ? numberOfRuns : 1;
            for (int runs = numberOfRuns; runs > 0; runs--)
            {
                router.Reset();
                
                await router.StartRouting<A,D,U>();

                routingTime += router.GetRoutingTime();
                uploadingTime += router.GetUploadingTime();
                downloadingTime += router.GetDownloadingTime();
                logger.Info("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                logger.Info("      Download Time: {0} (HH:MM:S.mS)", Helper.FormatElapsedTime(router.GetDownloadingTime()));
                logger.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            }

            routingTime = TimeSpan.FromMilliseconds(routingTime.TotalMilliseconds / numberOfRuns);
            uploadingTime = TimeSpan.FromMilliseconds(uploadingTime.TotalMilliseconds / numberOfRuns);
            downloadingTime = TimeSpan.FromMilliseconds(downloadingTime.TotalMilliseconds / numberOfRuns);

            routingTimes.Add(routingTime);
            uploadingTimes.Add(uploadingTime);
            downloadingTimes.Add(downloadingTime);
            

            var personas = router.GetPersonas();
            var computedRoutes = router.GetComputedRoutesCount();
            var uploadTest = await CheckUploadedRoutesAsync(personas, comparisonTable, computedRoutes);
            uploadResults.Add(uploadTest);

            
            benchmarkStopWatch.Stop();
            var executionTime = benchmarkStopWatch.Elapsed;
            var totalTime = Helper.FormatElapsedTime(benchmarkStopWatch.Elapsed);
            logger.Info("---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            logger.Info("Benchmark performed in {0} (HH:MM:S.mS) for the downloader '{1}' and the router '{2}' using the '{3}' algorithm", totalTime, typeof(D).Name, router.GetType().Name, typeof(A).Name);
            logger.Info("---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");

            return executionTime;
        }
    }
}