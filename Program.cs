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


            //logger.Info("Retrieving Node information");
            //graph.GetNodes();

            logger.Info("Testing point location");
            graph.TestClosestNode("Synapsis", 4.369250640244246, 50.82125803679304);

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}