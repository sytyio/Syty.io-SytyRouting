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

            //////////////////////////////////////////////
            // // // Create a new table for route results
            /////////////////////////////////////////////
            // var personaRouteTable = new DataBase.PersonaRouteTable(Configuration.PersonaTable,Configuration.PersonaRouteTable,Configuration.ConnectionString);
            // await personaRouteTable.CreateDataSet();
            /////////////////////////////////////////////


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


            TransportModes.DisplayMaskMap();

            //string[] requestedSequence = new string[] {"Foot"};
            //string[] requestedSequence = new string[] {"Bicycle","Foot", "Tram"};
            //string[] requestedSequence = new string[] {"Foot", "Public Transport"};
            //string[] requestedSequence = new string[] {"Car", "Public Transport"};
            //string[] requestedSequence = new string[] {"Car", "Foot", "Foot", "Bicycle", "Bicycle", "Public Transport", "Metro", "Public Transport"};
            string[] requestedSequence = new string[] {"Car", "Foot", "Foot", "Bicycle", "Bicycle", "Public Transport", "Metro", "Public Transport", "Car", "Tram", "Public Transport"};

            byte[] createdSequence = TransportModes.CreateSequence(requestedSequence);
            logger.Debug("Requested sequence: {0}", TransportModes.NamesToString(requestedSequence));
            logger.Debug("Created sequence: {0}", TransportModes.NamesToString(TransportModes.ArrayToNames(createdSequence)));


            // // Persona spatial data generation
            //await RoutingBenchmark.CreateDataSet();
            //var personaRouter = new PersonaRouter(graph, Configuration.PersonaRouteTable);

            //await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>();
    
            //personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}