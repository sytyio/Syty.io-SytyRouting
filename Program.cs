using System.Diagnostics;
using NLog;
using SytyRouting.Algorithms.Dijkstra;

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


            logger.Info("Testing point location");
            graph.TestClosestNode("Synapsis",4.369293555585981, 50.82126481464596);
            graph.TestClosestNode("Robinson", 4.3809799, 50.8045279);       

            logger.Info("Route searching using Dijkstra's algorithm");
            var dijkstra = new Dijkstra(graph); 
            // dijkstra.GetRoute(2135360285, 145351);
            dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            // dijkstra.GetRoute(2135360285, -145351);
            dijkstra.GetRoute(26913029, 401454717);


            // Logger flushing
            LogManager.Shutdown();
        }
    }
}