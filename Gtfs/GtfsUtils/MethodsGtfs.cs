using NLog;
using SytyRouting.Gtfs.ModelCsv;
using SytyRouting.Gtfs.ModelGtfs;
using NetTopologySuite.Geometries;

namespace SytyRouting.Gtfs.GtfsUtils
{
    public class MethodsGtfs
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static Dictionary<string, StopGtfs> createStopGtfsDictionary(List<StopCsv> recordsStop)
        {
            var stopDico = new Dictionary<string, StopGtfs>();
            foreach (StopCsv stop in recordsStop)
            {
                stopDico.Add(stop.Id, new StopGtfs(stop.Id, stop.Name, stop.Lat, stop.Lon));
            }
            return stopDico;
        }

        // Creates the routes but trips are empty
        public static Dictionary<string, RouteGtfs> createRouteGtfsDictionary(List<RouteCsv> recordsRoute)
        {
            var routeDico = new Dictionary<string, RouteGtfs>();
            foreach (RouteCsv route in recordsRoute)
            {
                routeDico.Add(route.Id, new RouteGtfs(route.Id, route.LongName, route.Type, new Dictionary<string, TripGtfs>()));

            }
            return routeDico;
        }

        // Create the shapes
        public static Dictionary<string, ShapeGtfs> createShapeGtfsDictionary(List<ShapeCsv> recordsShape)
        {

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
                if (!shapeBuff.ItineraryPoints.TryGetValue(shape.PtSequence, out pointBuff)) // Adds the point to the itinerary points
                {
                    shapeBuff.ItineraryPoints.Add(shape.PtSequence, new Point(shape.PtLon, shape.PtLat));
                }
            }
            return shapeDico;
        }

        public static Dictionary<string, ScheduleGtfs> createScheduleGtfsDictionary(List<StopTimesCsv> recordStopTime, Dictionary<string, StopGtfs> stopDico, Dictionary<string, TripGtfs> tripDico)
        {
            TripGtfs targetTrip = null;
            ScheduleGtfs schedule = null;
            StopGtfs stopBuff = null;

            // Create the timeStop with an dico details
            var scheduleDico = new Dictionary<string, ScheduleGtfs>();  // String = l'id du trip
            foreach (var stopTime in recordStopTime)
            {
                stopDico.TryGetValue(stopTime.StopId, out stopBuff);
                TimeSpan arrivalTime = MethodsCsv.ParseMore24Hours(stopTime.ArrivalTime);
                TimeSpan departureTime = MethodsCsv.ParseMore24Hours(stopTime.DepartureTime);
                StopTimesGtfs newStopTime = new StopTimesGtfs(stopBuff, arrivalTime, departureTime, stopTime.Sequence);
                // If a line already exists
                if (scheduleDico.TryGetValue(stopTime.TripId, out schedule))
                {
                    schedule.Details.Add(stopTime.Sequence, newStopTime);
                }
                else
                {
                    tripDico.TryGetValue(stopTime.TripId, out targetTrip);
                    Dictionary<int, StopTimesGtfs> myDico = new Dictionary<int, StopTimesGtfs>();
                    myDico.Add(stopTime.Sequence, newStopTime);
                    scheduleDico.Add(stopTime.TripId, new ScheduleGtfs(targetTrip.Id, myDico));
                }
            }
            return scheduleDico;
        }

        internal static void addTripsToRoute(Dictionary<string, TripGtfs> tripDico)
        {
            foreach (KeyValuePair<string, TripGtfs> trip in tripDico)
            {
                // add the current trip to the route
                var route = trip.Value.Route;
                var listTrips = route.Trips;
                TripGtfs buffTrips = null;
                if (!listTrips.TryGetValue(trip.Key, out buffTrips))
                {
                    listTrips.Add(trip.Key, trip.Value);
                }
            }
        }

        // Create a trip with a shape (if there's an available shape) and with no schedule
        public static Dictionary<string, TripGtfs> createTripGtfsDictionary(List<TripCsv> recordsTrip, Dictionary<string, ShapeGtfs> shapeDico, Dictionary<string, RouteGtfs> routeDico)
        {
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
                        newTrip = new TripGtfs(buffRoute, trip.Id, buffShape, null);
                    }
                    else
                    {
                        newTrip = new TripGtfs(buffRoute, trip.Id, null, null);
                    }

                    tripDico.Add(trip.Id, newTrip);
                }
            }
            return tripDico;
        }

        public static void addScheduleToTrip(Dictionary<string, ScheduleGtfs> scheduleDico, Dictionary<string, TripGtfs> tripDico)
        {
            TripGtfs tripBuff = null;
            foreach (var schedule in scheduleDico)
            {
                if (tripDico.TryGetValue(schedule.Key, out tripBuff))
                {
                    tripBuff.Schedule = schedule.Value;
                }
            }
        }

        public static void printTripDico(Dictionary<string, TripGtfs> tripDico)
        {
            foreach (KeyValuePair<string, TripGtfs> trip in tripDico)
            {
                logger.Info("Key = {0}, Value = {1}", trip.Key, trip.Value);
            }
        }

        public static void printRouteDico(Dictionary<string, RouteGtfs> routeDico)
        {
            foreach (KeyValuePair<string, RouteGtfs> route in routeDico)
            {
                logger.Info("Key = {0}, Value = {1}", route.Key, route.Value);
            }
        }

        public static void printShapeDico(Dictionary<string, ShapeGtfs> shapeDico)
        {
            foreach (var shape in shapeDico)
            {
                logger.Info("Key {0}, Value {1}", shape.Key, shape.Value);
            }
        }

        public static void printScheduleDico(Dictionary<string, ScheduleGtfs> scheduleDico)
        {
            foreach (var schedule in scheduleDico)
            {
                logger.Info("Key {0}, Value {1}", schedule.Key, schedule.Value);
            }
        }

        public static void printStopDico(Dictionary<string, StopGtfs> stopDico)
        {
            foreach (var stop in stopDico)
            {
                logger.Info("Key {0}, Value {1}", stop.Key, stop.Value);
            }
        }

        public static void printStopTimeForOneTrip(Dictionary<string, TripGtfs> tripDico, string tripId)
        {
            TripGtfs targetedTrip = null;
            tripDico.TryGetValue(tripId, out targetedTrip);
            logger.Info("Mon voyage {0} ", targetedTrip);
            logger.Info("Mon horaire pour un voyage");
            foreach (KeyValuePair<int, StopTimesGtfs> stopTime in targetedTrip.Schedule.Details)
            {
                logger.Info("Key {0}, Value {1}", stopTime.Key, stopTime.Value);
            }
        }
    }
}