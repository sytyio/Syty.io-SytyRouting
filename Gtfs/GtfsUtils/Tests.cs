using SytyRouting.Gtfs.ModelGtfs;
using SytyRouting.Gtfs.ModelCsv;
using NLog;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using SytyRouting.Model;

namespace SytyRouting.Gtfs.GtfsUtils
{
    public class Tests
    {
        public ControllerGtfs CtrlGtfs;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Tests(ControllerGtfs gtfs)
        {
            CtrlGtfs = gtfs;
        }

        public Tests(){
            
        }
        // public void PrintTripDico()
        // {
        //     foreach (KeyValuePair<string, TripGtfs> trip in CtrlGtfs.TripDico)
        //     {
        //         logger.Info("Key = {0}, Value = {1}", trip.Key, trip.Value);
        //     }
        // }

        // public void PrintRouteDico()
        // {
        //     foreach (KeyValuePair<string, RouteGtfs> route in CtrlGtfs.RouteDico)
        //     {
        //         logger.Info("Key = {0}, Value = {1}", route.Key, route.Value);
        //     }
        // }

        internal void PrintRecordsAgency()
        {
            foreach (var agency in CtrlGtfs.CtrlCsv.RecordsAgency)
            {
                logger.Info(agency);
            }
        }

        internal void PrintRecordsRoute()
        {
            foreach (var route in CtrlGtfs.CtrlCsv.RecordsRoute)
            {
                logger.Info(route);
            }
        }

        // public void PrintCalendarDico()
        // {
        //     foreach (KeyValuePair<string, CalendarGtfs> calendar in CtrlGtfs.CalendarDico)
        //     {
        //         var cal = calendar.Value.Days;
        //         string myString = "";
        //         for (int i = 0; i < cal.Count(); i++)
        //         {
        //             myString += cal[i] + " ";
        //         }
        //         logger.Info("Key = {0}, Value = {1}", calendar.Key, myString);
        //     }
        // }

        // public void PrintShapeDico()
        // {
        //     foreach (var shape in CtrlGtfs.ShapeDico)
        //     {
        //         logger.Info("Key {0}, Value {1}", shape.Key, shape.Value);
        //     }
        // }

        // public void PrintScheduleDico()
        // {
        //     foreach (var schedule in CtrlGtfs.ScheduleDico)
        //     {
        //         logger.Info("Key {0}, Value {1}", schedule.Key, schedule.Value);
        //     }
        // }

        // public void PrintAgencyDico()
        // {
        //     foreach (var agency in CtrlGtfs.AgencyDico)
        //     {
        //         logger.Info("Key {0}, Value {1}", agency.Key, agency.Value);
        //     }
        // }

        // public void PrintStopDico()
        // {
        //     foreach (var stop in CtrlGtfs.StopDico)
        //     {
        //         logger.Info("Key {0}, Value {1}", stop.Key, stop.Value);
        //     }
        // }

        // public void PrintStopTimeForOneTrip(string tripId)
        // {
        //     TripGtfs targetedTrip = CtrlGtfs.TripDico[tripId];
        //     logger.Info("My trip {0} ", targetedTrip);
        //     logger.Info("My schedule for one trip");
        //     if (targetedTrip.Schedule != null)
        //     {
        //         foreach (KeyValuePair<int, StopTimesGtfs> stopTime in targetedTrip.Schedule.Details)
        //         {
        //             logger.Info("Key {0}, Value {1}", stopTime.Key, stopTime.Value);
        //         }
        //     }
        // }

        public void PrintNumberTripsSunday()
        {
            var test3 = CtrlGtfs.SelectAllTripsForGivenDay(6);
            logger.Info("Sunday {0}", test3.Count);

            var test = CtrlGtfs.SelectAllTripsForGivenDayAndBetweenGivenHours(new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0), 6);
            logger.Info("Sunday between 10:00:00 and 11:00:00 {0} " + test.Count);

            var test4 = CtrlGtfs.SelectAllTripsForGivenDayAndBetweenGivenHours(new TimeSpan(11, 0, 1), new TimeSpan(23, 59, 59), 6);
            logger.Info("Sunday between 11:00:01 and 23:59:59 {0} " + test4.Count);

            var test5 = CtrlGtfs.SelectAllTripsForGivenDayAndBetweenGivenHours(new TimeSpan(0, 0, 0), new TimeSpan(9, 59, 59), 6);
            logger.Info("Sunday between 00:00:00 and 09:59:59 {0} " + test5.Count);

            int allIn = test.Count + test4.Count + test5.Count;
            logger.Info("Sum of 0-10, 10-11, 11-00 {0} ", allIn);
            logger.Info("Sum of parts equals all {0} ", test3.Count == allIn);
        }

        public void PrintAllEdges()
        {
            foreach (var edge in CtrlGtfs.GetEdges())
            {
                logger.Info("Edge {0}", edge);
            }
        }

        public void PrintDistinctShapesForOneTrip(RouteCsv chosenRoute)
        {
            var nbParRoute = CtrlGtfs.CtrlCsv.RecordsTrip.FindAll(x => x.RouteId == chosenRoute.Id).GroupBy(x => x.ShapeId).Select(x => Tuple.Create(x.Key, x.Count()));
            foreach (var item in nbParRoute)
            {
                logger.Info("shape_id {0}, number of use {1}", item.Item1, item.Item2);
            }

            var tripsForChosenRoute = CtrlGtfs.CtrlCsv.RecordsTrip.FindAll(x => x.RouteId == chosenRoute.Id);
            logger.Info("Number of distinct trip for one route {0}", tripsForChosenRoute.Count());
            logger.Info("Id of the chosen route {0} and name {1}", chosenRoute.Id, chosenRoute.LongName);
        }

        public void PrintArray(object[] array)
        {
            for (int i = 0; i < array.Count(); i++)
            {
                logger.Info(array[i]);
            }
        }

        /**Returns a list of arrays of 1 or 2 double
    If there is a shape:
        - the first double is the distance between the two points nearest on the linestring for the two stops
        - the second double is the distance between the first stop and the nearest point on the linestring
       
    If there is no shape : 
        - the only double is the distance between the two stops

 */
        // public List<double[]> GetAllDistancesForOnTrip(List<Point> pointsForOneTrip,
        //                                                TripCsv chosenTripForChosenRoute
        //                                                     )
        // {
        //     List<double[]> distancesForOneTrip = new List<double[]>();
        //     // If there is no given shape, calculate the distance between 2 stops based on their coordinates
        //     if (chosenTripForChosenRoute.ShapeId==null)
        //     {
        //         logger.Info("No shapes available");
        //         return ListOfPointsToListOfDistance(pointsForOneTrip);
        //     }
        //     // If there is a shape, calculate the distance between 2 points of the shape
        //     //  These two points are the closest to two consecutive stops
        //     else
        //     {
        //         LineString lineString = CtrlGtfs.CreateLineString(chosenTripForChosenRoute.ShapeId);
        //         for (int i = 0; i < pointsForOneTrip.Count - 1; i++)
        //         {
        //             Coordinate[] coordinateA = DistanceOp.NearestPoints(lineString, pointsForOneTrip[i]);
        //             Coordinate[] coordinateB = DistanceOp.NearestPoints(lineString, pointsForOneTrip[i + 1]);
        //             Point pointA = new Point(coordinateA[0]);
        //             Point pointB = new Point(coordinateB[0]);
        //             double[] arrayOfDistances = new double[2];

        //             arrayOfDistances[0] = Helper.GetDistance(pointA.X, pointA.Y, pointB.X, pointB.Y);
        //             arrayOfDistances[1] = Helper.GetDistance(pointA.X, pointA.Y, pointsForOneTrip[i].X, pointsForOneTrip[i].Y);

        //             distancesForOneTrip.Add(arrayOfDistances);
        //             logger.Debug("Infos : ");
        //             logger.Debug("the distance between the first stop and the nearest point on the linestring {0}", distancesForOneTrip[i][1]);
        //             logger.Debug("Distance between the two nearest point on linestring {0}", distancesForOneTrip[i][0]);
        //             logger.Debug("Distance between the 2 intial stops {0}", Helper.GetDistance(pointsForOneTrip[i].X, pointsForOneTrip[i].Y, pointsForOneTrip[i + 1].X, pointsForOneTrip[i + 1].Y));
        //         }
        //         return distancesForOneTrip;
        //     }
        // }

        public List<double[]> ListOfPointsToListOfDistance(List<Point> pointsForOneTrip)
        {
            List<double[]> distances = new List<double[]>();
            for (int i = 0; i < pointsForOneTrip.Count - 1; i++)
            {
                double[] arrayOfDistances = { DistanceBetweenTwoPoint(pointsForOneTrip[i], pointsForOneTrip[i + 1]) };
                distances.Add(arrayOfDistances);
            }
            return distances;
        }

        public double DistanceBetweenTwoPoint(Point point1, Point point2)
        {
            return Helper.GetDistance(point1.X, point1.Y, point2.X, point2.Y);
        }

        // public double[] DistancesBetweenTwoPointNearestLineString
        //    (Point stop1, Point stop2, string shapeId, List<ShapeCsv> recordsShape)
        // {
        //     if (recordsShape.Count == 0)
        //     {
        //         logger.Info("No shapes available");
        //         throw new Exception("No shapes available ");
        //     }
        //     LineString lineString = CtrlGtfs.CreateLineString(shapeId);
        //     // logger.Info(lineString);
        //     Coordinate[] coordinateA = DistanceOp.NearestPoints(lineString, stop1);
        //     Coordinate[] coordinateB = DistanceOp.NearestPoints(lineString, stop2);
        //     Point stop1OnLineString = new Point(coordinateA[0]);
        //     Point stop2ONLineString = new Point(coordinateB[0]);
        //     double[] arrayOfDistances = new double[4];
        //     /**
        //         0 : between two stops
        //         1 : between stop1 and linestring
        //         2 : between stop2 and linestring
        //         3 : between the two points on linestring
        //     */
        //     arrayOfDistances[0] = Helper.GetDistance(stop1.X, stop1.Y, stop2.X, stop2.Y);
        //     arrayOfDistances[1] = Helper.GetDistance(stop1.X, stop1.Y, stop1OnLineString.X, stop1OnLineString.Y);
        //     arrayOfDistances[2] = Helper.GetDistance(stop2.X, stop2.Y, stop2ONLineString.X, stop2ONLineString.Y);
        //     arrayOfDistances[3] = Helper.GetDistance(stop1OnLineString.X, stop1OnLineString.Y, stop2ONLineString.X, stop2ONLineString.Y);
        //     return arrayOfDistances;
        // }

        // public List<double> ListOfStopsTimeToListOfTimes(List<StopTimesCsv> listStopsTime)
        // {
        //     int size = listStopsTime.Count;
        //     List<double> allTimes = new List<double>();
        //     for (int i = 0; i < size - 1; i++)
        //     {
        //         allTimes.Add(TimeBetweenTwoStops(listStopsTime[i], listStopsTime[i + 1]));
        //     }
        //     return allTimes;
        // }

        // public double TimeBetweenTwoStops(StopTimesCsv departureStop, StopTimesCsv arrivalStop)
        // {
        //     TimeSpan departureTimeStop1;
        //     TimeSpan arrivalTimeStop2;
        //     try
        //     {
        //         departureTimeStop1 = TimeSpan.Parse(departureStop.DepartureTime!);
        //     }
        //     catch (System.OverflowException)
        //     {
        //         departureTimeStop1 = CtrlGtfs.ParseMore24Hours(departureStop.DepartureTime!);
        //     }
        //     try
        //     {
        //         arrivalTimeStop2 = TimeSpan.Parse(arrivalStop.ArrivalTime!);
        //     }
        //     catch (System.OverflowException)
        //     {
        //         arrivalTimeStop2 = CtrlGtfs.ParseMore24Hours(arrivalStop.ArrivalTime!);
        //     }
        //     double time = (arrivalTimeStop2 - departureTimeStop1).TotalSeconds;
        //     logger.Info("Départure time {0}, Arrival time {1}, DurationS {2}", departureTimeStop1, arrivalTimeStop2, time);
        //     return time;
        // }

        // public void PrintStopsWithEdges()
        // {
           
        //     foreach (var stop in CtrlGtfs.GetNodes())
        //     {
        //          logger.Info("/////////------------{0}-------------///////",stop);
        //         logger.Info("Inwards {0}", stop.InwardEdges.Count);

        //         foreach (var edge in stop.InwardEdges)
        //         {
        //             logger.Info("S = {0}, T = {1}",edge.SourceNode, edge.TargetNode);
        //         }
        //         logger.Info("Outwards {0}", stop.OutwardEdges.Count);
        //         foreach (var edge in stop.OutwardEdges)
        //         {
        //             logger.Info("S = {0}, T = {1}",edge.SourceNode, edge.TargetNode);
        //         }
        //     }
        // }
  public async Task GraphData(){
            var graph = new Graph();
            await graph.FileLoadAsync("graph.dat");
            graph.TraceNodes();
            var personaRouter = new PersonaRouter(graph);
            int cptNodes = graph.GetNodeCount();

            logger.Info("Nb nodes {0}",graph.GetNodeCount());

            var listProviders = new List<ProviderCsv>();
            listProviders.Add(ProviderCsv.stib);
            listProviders.Add(ProviderCsv.ter);
            // listProviders.Add(ProviderCsv.tur);
            // listProviders.Add(ProviderCsv.tec);
            graph.GetDataFromGtfs(listProviders, cptNodes);
            var listsNode = new Dictionary<ProviderCsv,IEnumerable<Node>>();
            var listsEdge = new Dictionary<ProviderCsv,IEnumerable<Edge>>();
            foreach(var gtfs in graph.GtfsDico){
                listsNode.Add(gtfs.Key,gtfs.Value.GetNodes());
                listsEdge.Add(gtfs.Key,gtfs.Value.GetEdges());
                
            }
            logger.Info("Lists node size {0}", listsNode.Count()); 
            logger.Info("Lists edge size {0}", listsEdge.Count());

            foreach(var item in listsNode){
                logger.Info("///////////////////////");
                foreach (var node in item.Value){
                    logger.Info("Id node {0}, S= {1}, T= {2}, nb arêtes entrantes = {3}, nb arêtes sortantes {4}",node.Idx, node.ValidSource,node.ValidTarget, node.InwardEdges.Count,node.OutwardEdges.Count);
                    // foreach(var inEdge in node.InwardEdges){
                    //     logger.Info("{0} , {1}, {2} ",  inEdge.SourceNode, inEdge.TargetNode, inEdge.MaxSpeedMPerS);
                    // }
                }
            }
        }

        // public void  PrintOneShapeStib(ControllerGtfs gtfs){
        //     var shape = gtfs.ShapeDico["210b0166"]; 
        //     foreach(var elem2 in shape.SplitLineString){
        //         logger.Info(elem2);
        //     }
        // }

        // public void  PrintOneShapeTec(ControllerGtfs gtfs){
        //     var shape = gtfs.ShapeDico["X16220026"];
        //     foreach(var elem2 in shape.SplitLineString){
        //         logger.Info(elem2);
        //     }
        // }
    }
}