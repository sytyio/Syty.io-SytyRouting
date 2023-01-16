using NLog;
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

            // ========================================
            // // Npgsql plugin to interact with spatial data provided by the PostgreSQL PostGIS extension
            NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();

            logger.Info("syty.io routing engine for large scale datasets");

            logger.Info("Creating syty.io routing graph from dataset");
            var graph = new Graph();
            await graph.FileLoadAsync(Configuration.GraphFileName);

            // logger.Info("Count = {0}", graph.GetNodes().Count()); // 1558439 
            // for (int i = 0  ; i < graph.GetNodes().Count(); i++)
            // {
            //     var node = graph.GetNodes()[i];
            //     foreach(var data in node.InwardEdges){
            //         if(data.LengthM<=0||data.MaxSpeedMPerS<=0||Double.IsNaN(data.LengthM)||data.MaxSpeedMPerS>50){
            //             logger.Info("Type {0}, speed {1}, length {1}",TransportModes.MaskToString(data.TransportModes),data.MaxSpeedMPerS,data.LengthM);
            //         }
            //     }
            //     foreach(var data in node.OutwardEdges){
            //         if(data.LengthM<=0||data.MaxSpeedMPerS<=0||Double.IsNaN(data.LengthM)||data.MaxSpeedMPerS>50){
            //             logger.Info("Type {0}, speed {1}, length {1}",TransportModes.MaskToString(data.TransportModes),data.MaxSpeedMPerS,data.LengthM);
            //         }
            //     }
            // }

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


            // // Persona spatial data generation
            await RoutingBenchmark.CreateDataSet();
            var personaRouter = new PersonaRouter(graph);

            await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>();


    // 10 000 personas test:
    //    SytyRouting.PersonaRouter.DBRouteBenchmarkUploadAsync#561   | DEBUG | Transport sequence validation errors: 208 (2.077506991610068 % of the requested transport sequences were overridden)
    //    SytyRouting.PersonaRouter.DBRouteBenchmarkUploadAsync#562   |  INFO | 9942 Routes successfully uploaded to the database in 0.00:14:50.750 (d.hh:mm:s.ms)
    //    SytyRouting.PersonaRouter.DBRouteBenchmarkUploadAsync#563   | DEBUG | 70 routes (out of 10012) failed to upload (0.6991610067918498 %)
    //    SytyRouting.PersonaRouter.DBRouteBenchmarkUploadAsync#564   | DEBUG | 'Origin = Destination' errors: 52 (0.519376747902517 %)
    //    SytyRouting.PersonaRouter.DBRouteBenchmarkUploadAsync#565   | DEBUG |                  Other errors: 18 (0.1797842588893328 %)
    //                   SytyRouting.PersonaRouter.StartRouting#92    |  INFO | StartRouting execution time :: 0.00:18:28.007
    
            //personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}