using NLog;
using Npgsql;
using NetTopologySuite.Geometries;


namespace SytyRouting
{
    using Gtfs.ModelCsv;
    using Gtfs.ModelGtfs;
    using Gtfs.GtfsUtils;

    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            // Logger configuration
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            NLog.Common.InternalLogger.LogToConsole = false;

            //  await MethodsGtfs.DownloadsGtfs();

            // The chosen provider
            ProviderCsv choice = ProviderCsv.stib;


            // Data of the provider
            List<StopCsv> recordsStop = MethodsCsv.GetAllStops(choice);
            List<RouteCsv> recordsRoute = MethodsCsv.GetAllRoutes(choice);
            List<TripCsv> recordsTrip = MethodsCsv.GetAllTrips(choice);
            List<ShapeCsv> recordsShape = MethodsCsv.GetAllShapes(choice);
            List<StopTimesCsv> recordStopTime = MethodsCsv.GetAllStopTimes(choice);


            // Create the StopGtfs objects
            var stopDico = new Dictionary<string, StopGtfs>();
            foreach (StopCsv stop in recordsStop)
            {
                stopDico.Add(stop.Id, new StopGtfs(stop.Id, stop.Name, stop.Lat, stop.Lon));
            }

            // Create the routeGtfs objects with an empty trips dictionary
            var routeDico = new Dictionary<string, RouteGtfs>();
            foreach (RouteCsv route in recordsRoute)
            {
                routeDico.Add(route.Id, new RouteGtfs(route.Id, route.LongName, route.Type, new Dictionary<string, TripGtfs>()));

            }


            // Create the shape with an empty itinerary points
            var shapeDico = new Dictionary<string, ShapeGtfs>();
            foreach (var shape in recordsShape)
            {
                ShapeGtfs shapeBuff = null;
                if (!shapeDico.TryGetValue(shape.Id, out shapeBuff))
                {
                    shapeDico.Add(shape.Id, new ShapeGtfs(shape.Id, new Dictionary<int, Point>(), MethodsCsv.CreateLineString(recordsShape, shape.Id)));
                    shapeDico.TryGetValue(shape.Id, out shapeBuff);
                }

                Point pointBuff = null;
                if (!shapeBuff.ItineraryPoints.TryGetValue(shape.PtSequence, out pointBuff))
                {
                    shapeBuff.ItineraryPoints.Add(shape.PtSequence, new Point(shape.PtLon, shape.PtLat));
                }
            }



            // Create the tripGtfs with a route and a shape (if there is a shape)
            var tripDico = new Dictionary<string, TripGtfs>();
            RouteGtfs buffRoute = null;
            ShapeGtfs buffShape = null;
            foreach (TripCsv trip in recordsTrip)
            {
                if (routeDico.TryGetValue(trip.RouteId, out buffRoute))
                {
                    TripGtfs newTrip;
                    if (trip.ShapeId != null && shapeDico.TryGetValue(trip.ShapeId, out buffShape))
                    {
                        newTrip = new TripGtfs(buffRoute, trip.Id, buffShape);
                    }
                    else
                    {
                        newTrip = new TripGtfs(buffRoute, trip.Id, null);
                    }

                    tripDico.Add(trip.Id, newTrip);
                }
            }


            foreach (KeyValuePair<string, TripGtfs> trip in tripDico)
            {
                // La route mentionnée dans trip ne contient pas encore l'id de trip dans ses trips, l'ajouter
                var route = trip.Value.Route;
                var listTrips = route.Trips;
                TripGtfs buffTrips = null;
                if (!listTrips.TryGetValue(trip.Key, out buffTrips))
                {
                    listTrips.Add(trip.Key, trip.Value);
                }
            }

            TripGtfs targetTrip = null;
            string idTargetTrip = "115712124247127001";
            logger.Info(targetTrip);
            StopTimesGtfs stopTimes = null;

            // Create the timeStop with an dico details
            var stopTimesDico = new Dictionary<string, StopTimesGtfs>();  // String = l'id du trip
            foreach (var stopTime in recordStopTime)
            {
                    if(!stopTimesDico.TryGetValue(stopTime.TripId, out stopTimes)){
                        tripDico.TryGetValue(stopTime.TripId, out targetTrip);
                         // Si la clé est pas encore présente
                        stopTimesDico.Add(stopTime.TripId,new StopTimesGtfs(targetTrip,new Dictionary<int, ScheduleGtfs>())); // A finir 
                    }
            }

            // StopTimes
            foreach (var stopTime in stopTimesDico)
            {
                logger.Info("Key {0}, Value {1}", stopTime.Key, stopTime.Value);
            }


            // // Shape
            // foreach (var shape in shapeDico)
            // {
            //     logger.Info("Key {0}, Value {1}", shape.Key, shape.Value);
            // }



            // // trip
            // foreach (KeyValuePair<string, TripGtfs> trip in tripDico)
            // {
            //     logger.Info("Key = {0}, Value = {1}", trip.Key, trip.Value);
            // }


            // // Routes
            // foreach (KeyValuePair<string, RouteGtfs> route in routeDico)
            // {
            //     logger.Info("Key = {0}, Value = {1}", route.Key, route.Value);
            // }



            // Stops
            // foreach(KeyValuePair<string,StopGtfs> stop in stopDico){
            //     logger.Info("Key = {0}, Value = {1}", stop.Key, stop.Value);
            // }


            //MethodsGtfs.CleanGtfs();


            // ========================================
            // // Npgsql plugin to interact with spatial data provided by the PostgreSQL PostGIS extension
            // NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();




            // logger.Info("syty.io routing engine for large scale datasets");

            // logger.Info("Creating syty.io routing graph from dataset");
            // var graph = new Graph();
            // await graph.FileLoadAsync("graph.dat");

            // // graph.TraceNodes();


            // // // Benchmarking.PointLocationTest(graph);

            // // // Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.Dijkstra.Dijkstra>(graph);

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

            // // // //Benchmarking.RoutingAlgorithmBenchmarking<SytyRouting.Algorithms.HeuristicDijkstra.HeuristicDijkstra>(graph);


            // // Persona spatial data generation
            // var personaRouter = new PersonaRouter(graph);
            // await personaRouter.StartRouting<SytyRouting.Algorithms.Dijkstra.Dijkstra>();
            // // personaRouter.TracePersonas();
            // // personaRouter.TracePersonasRouteResult();


            // Logger flushing
            LogManager.Shutdown();
        }
    }


}