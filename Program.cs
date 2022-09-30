using NLog;
using Npgsql;
using NetTopologySuite.Geometries;


namespace SytyRouting
{
    using Gtfs.ModelCsv;
    using Gtfs.ModelGtfs;
    using Gtfs.GtfsUtils;

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Logger configuration
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            NLog.Common.InternalLogger.LogToConsole = false;

            //  await MethodsSetUp.DownloadsGtfs();

            // The chosen provider
            ProviderCsv choice = ProviderCsv.ter;


            // Data of the provider
            List<StopCsv> recordsStop = MethodsCsv.GetAllStops(choice);
            List<RouteCsv> recordsRoute = MethodsCsv.GetAllRoutes(choice);
            List<TripCsv> recordsTrip = MethodsCsv.GetAllTrips(choice);
            List<ShapeCsv> recordsShape = MethodsCsv.GetAllShapes(choice);
            List<StopTimesCsv> recordStopTime = MethodsCsv.GetAllStopTimes(choice);


            // Create the Gtfs objects
            var stopDico = MethodsGtfs.createStopGtfsDictionary(recordsStop);
            var routeDico = MethodsGtfs.createRouteGtfsDictionary(recordsRoute);
            var shapeDico = MethodsGtfs.createShapeGtfsDictionary(recordsShape);
            var tripDico = MethodsGtfs.createTripGtfsDictionary(recordsTrip, shapeDico, routeDico);
            MethodsGtfs.addTripsToRoute(tripDico);
            var scheduleDico = MethodsGtfs.createScheduleGtfsDictionary(recordStopTime, stopDico, tripDico);
            MethodsGtfs.addScheduleToTrip(scheduleDico,tripDico);


            // Some prints
            string tripId = "OCESN105330F223219:2022-09-27T00:33:07Z";
            MethodsGtfs.printStopTimeForOneTrip(tripDico,tripId);
          //  MethodsGtfs.printTripDico(tripDico);
            
            //MethodsSetUp.CleanGtfs();


            // ========================================
            // // Npgsql plugin to interact with spatial data provided by the PostgreSQL PostGIS extension
            // NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();




            // logger.Info("syty.io routing engine for large scale datasets");

            // logger.Info("Creating syty.io routing graph from dataset");
            // var graph = new Graph();
            // await graph.FileLoadAsync("graph.dat");

            // // graph.TraceNodes();


            // // // Benchmarking.PointLocationTest(graph);

            // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);

            // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);
            // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);

            // // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BackwardDijkstra.BackwardDijkstra>(graph);
            // // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph);

            // // // // Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            // // // //                                                    SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph);

            // // // // Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            // // // //                                                    SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);

            // // // //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            // // //                                                    //SytyRouting.Algorithms.ArrayDijkstra.ArrayDijkstra>(graph);

            // // // //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);


            // // Persona spatial data generation
            // var personaRouter = new PersonaRouter(graph);
            // await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>();
            // // personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();


            // Logger flushing
            LogManager.Shutdown();
        }
    }


}