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
            NLog.Common.InternalLogger.LogToConsole = true;

            logger.Info("syty.io routing engine for large scale datasets");

            // Console.WriteLine("syty.io routing engine for large scale datasets");

            var graph = new Graph();
            await graph.DBLoadAsync();
            graph.GetNodes();

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}