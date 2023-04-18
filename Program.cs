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



            ////////////////////////////////////////////////
            // //  Create a new table for route results   //
            ////////////////////////////////////////////////
            await DataBase.PersonaRouteFullTable.Create();
            ////////////////////////////////////////////////


            ///////////////////////////////////////////////////////////////////////////////////////////////
            // //                        Set PLGSQL functions on DB                                   // //
            ///////////////////////////////////////////////////////////////////////////////////////////////
            var connectionString = Configuration.ConnectionString;
            await DataBase.PLGSQLFunctions.SetCoaleaseTransportModesTimeStampsFunction(connectionString);
            ///////////////////////////////////////////////////////////////////////////////////////////////



            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // //                                           Routing algorithms benchmarking                                                      // //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            var transportSequence=TransportModes.NamesToArray(Configuration.DefaultBenchmarkSequence);
            Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph,transportSequence);


            //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BackwardDijkstra.BackwardDijkstra>(graph,transportSequence);
            //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph,transportSequence);

            //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.BidirectionalDijkstra.BidirectionalDijkstra>(graph,transportSequence);

            //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph,transportSequence);

            //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.ArrayDijkstra.ArrayDijkstra>(graph,transportSequence);

            //Benchmarking.MultipleRoutingAlgorithmsBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra,
            //                                                    SytyRouting.Algorithms.ArrayDijkstra.ArrayDijkstra>(graph,transportSequence);

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            // //             Multimodal Persona spatial data generation and routing benchmarking             // //
            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            //await Routing.MultimodalBenchmarking.Start(graph);
            ///////////////////////////////////////////////////////////////////////////////////////////////////////



            ///////////////////////////////////////////////////////
            // //    Route results uploading benchmarking     // //
            ///////////////////////////////////////////////////////
            //await DataBase.RouteUploadBenchmarking.Start(graph);
            ///////////////////////////////////////////////////////



            // //////////////



            // Logger flushing
            LogManager.Shutdown();
        }
    }
}