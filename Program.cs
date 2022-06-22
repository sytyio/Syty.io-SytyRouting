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
            graph.TestClosestNode("Synapsis",4.369293555585981, 50.82126481464596);
            graph.TestClosestNode("Robinson", 4.3809799, 50.8045279);

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}