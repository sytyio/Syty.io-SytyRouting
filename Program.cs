using NLog;
using Npgsql;
using NetTopologySuite.Geometries;
using SytyRouting.Model;


namespace SytyRouting
{
    using Gtfs.GtfsUtils;
    using Gtfs.ModelGtfs;
    using Gtfs.ModelCsv;

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Logger configuration
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            NLog.Common.InternalLogger.LogToConsole = false;

            // ========================================
            // // Npgsql plugin to interact with spatial data provided by the PostgreSQL PostGIS extension
            NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();

            logger.Info("syty.io routing engine for large scale datasets");

            logger.Info("Creating syty.io routing graph from dataset");
            var graph = new Graph();
            await graph.FileLoadAsync(Configuration.GraphFileName);


            //graph.TraceNodes();

             graph.TraceOneNode(graph.GetNodes()[0]);
             graph.TraceOneNode(graph.GetNodes()[1]);
              graph.TraceOneNode(graph.GetNodes()[1558438]);
             graph.TraceOneNode(graph.GetNodes()[1558439]);
             graph.TraceOneNode(graph.GetNodes()[1558449]);
            //  graph.TraceOneNode(graph.GetNodes()[1559000]);
            //  graph.TraceOneNode(graph.GetNodes()[1559700]);
            //  graph.TraceOneNode(graph.GetNodes()[1560000]);
            //  graph.TraceOneNode(graph.GetNodes()[1561286]);
            //  graph.TraceOneNode(graph.GetNodes()[1561287]);
            //  graph.TraceOneNode(graph.GetNodes()[1561306]);

            // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);

            // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);

            // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);
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

            // // //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            // //                                                    //SytyRouting.Algorithms.ArrayDijkstra.ArrayDijkstra>(graph);

            // // //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);


            // Persona spatial data generation
            var personaRouter = new PersonaRouter(graph);

            string transportModeName = "Car";
            byte transportMode = Helper.GetTransportModeMask(transportModeName);

            if(transportMode != 0)
                await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>(transportMode);
            else
                logger.Info("Unable to compute route for the {0} tranport mode", transportModeName);
            personaRouter.TracePersonas();
            personaRouter.TracePersonasRouteResult();
           

            // Logger flushing
            LogManager.Shutdown();
        }


    }
}