using NLog;
using Npgsql;
using NetTopologySuite.Geometries;


namespace SytyRouting
{
    using Gtfs.GtfsUtils;
    using Gtfs.ModelGtfs;

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Logger configuration
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            NLog.Common.InternalLogger.LogToConsole = false;


                Tests tests  = new Tests();

                tests.PrintAllEdges();
                logger.Info("Nb edge = "+tests.CtrlGtfs.EdgeDico.Count());
                logger.Info("Nb stops = "+tests.CtrlGtfs.StopDico.Count());

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