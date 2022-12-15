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

            // // logger.Info("Available public transport modes: {0}", TransportModes.NamesToString(Configuration.PublicTransportModes));

            // // string[] requestedSequence = new string[] {"Foot", "Bus", "Tram", "Car", "Train", "Foot", "Metro", "Bicycle", "Bus", "Foot"};
            // // string[] requestedSequence = new string[] {"Foot", "Car", "Train", "Foot", "Metro", "Bicycle", "Bus", "Foot"};
            string[] requestedSequence = new string[] {"Foot","Bus","Foot", "Tram","Foot", "Metro", "Foot","Train"};
            // string[] requestedSequence = new string[] {"Foot"};
            // string[] requestedSequence = new string[] {"Bus"};
            // string[] requestedSequence = new string[] {"Tram"};
            // string[] requestedSequence = new string[] {"Metro"};
            // string[] requestedSequence = new string[] {"Train"};
            // string[] requestedSequence = new string[] {"Bicycle"};
            // string[] requestedSequence = new string[] {"Car"};
            // string[] requestedSequence = new string[] {""};

            // string[] requestedSequence = new string[] {"Foot", "Bicycle", "Foot", "Car", "Foot"};


            // string[] requestedSequence = new string[] {"Foot", "Bicycle", "Car", "Foot"};

            // byte[] transportModesSequence = TransportModes.CreateTransportModeSequence(requestedSequence);
            byte[] transportModesSequence = TransportModes.NameSequenceToMasksArray(requestedSequence);
            // byte[] transportModesSequence = TransportModes.MergePublicTransportSequences(TransportModes.NameSequenceToMasksArray(requestedSequence));
            // byte[] transportModesSequence = new byte[2] {1, TransportModes.ArrayToMask(TransportModes.NameSequenceToMasksArray(requestedSequence))};

            logger.Info("Computing routes for the sequence: {0}", TransportModes.NamesToString(TransportModes.ArrayToNames(transportModesSequence)));

            //graph.TraceNodesByTransportMode(TransportModes.ArrayToMask(TransportModes.NameSequenceToMasksArray(requestedSequence)),10);

            await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>(transportModesSequence);
    
            personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();


            // Logger flushing
            LogManager.Shutdown();
        }
    }
}