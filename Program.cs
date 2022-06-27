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
            // dijkstra.GetRoute(26913029, 1486032529);
            // dijkstra.GetRoute(26913029, 7911022011);
            // dijkstra.GetRoute(2135360285, -145351);
            // dijkstra.GetRoute(26913029, 401454717);

            // logger.Info("Route searching using Dijkstra's algorithm based on coordinates");
            // From: Synapsis (4.369293555585981, 50.82126481464596)
            // To: Robinson  (4.3809799, 50.8045279)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3809799, 50.8045279);
            // To: Place Bara (4.3360253, 50.8396486)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3360253, 50.8396486);
            // To: National Basilica of the Sacred Heart (4.3178727, 50.8667117)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3178727, 50.8667117);
            // To: Kasteel van Beersel, Beersel (4.3003831, 50.7664786)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.3003831, 50.7664786);
            // To: Sint-Niklaaskerk, Liedekerke (4.0827609, 50.8706934)
            // dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 4.0827609, 50.8706934);
            // To: De Panne Markt, De Panne (2.5919885, 51.0990340)
            dijkstra.GetRoute(4.369293555585981, 50.82126481464596, 2.5919885, 51.0990340);



            // Logger flushing
            LogManager.Shutdown();
        }
    }
}