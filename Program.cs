using System.Diagnostics;
using NLog;

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
            

            logger.Info("Retrieving Node information");
            graph.GetNodes();


            // Try Dijkstra's algorithm
            var dijkstra = new DijkstraTest(graph.GetNodes(), 26913029, 20);

            logger.Info("Retrieving Node information from the reduced dataset");
            dijkstra.GetNodes();

            logger.Info("Retrieving Dijkstra step information");
            dijkstra.GetRoute(2135360285, 145351);
            // dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}