using NLog;
// using SytyRouting.Algorithms;
using Npgsql;

namespace SytyRouting
{
    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Logger configuration
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            NLog.Common.InternalLogger.LogToConsole = false;

            logger.Info("syty.io routing engine for large scale datasets");

            logger.Info("Creating syty.io routing graph from dataset");
            var graph = new Graph();
            await graph.FileLoadAsync("graph.dat");


            // Benchmarking.PointLocationTest(graph);

            // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);
            // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BackwardDijkstra.BackwardDijkstra>(graph);
            // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph);

            // Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph);

            // Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);

            //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
                                                               //SytyRouting.Algorithms.ArrayDijkstra.ArrayDijkstra>(graph);

            //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);


            // Persona spatial data generation
            var personaRouter = new PersonaRouter(graph);
            await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>();
            // personaRouter.TracePersonas();
            // personaRouter.TracePersonasIds();
            
            // personaRouter.TraceSortedPersonasIds();
            

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}