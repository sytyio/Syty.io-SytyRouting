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
            var s = new Stopwatch();
            s.Start();
            await graph.DBLoadAsync();
            var t1 = s.ElapsedMilliseconds;
            await graph.FileSaveAsync();
            var t2 = s.ElapsedMilliseconds-t1;
            await graph.FileLoadAsync();
            var t3 = s.ElapsedMilliseconds-t2-t1;
            s.Stop();
            

            logger.Info("Retrieving Node information");
            graph.GetNodes();

            // Logger flushing
            LogManager.Shutdown();
        }
    }
}