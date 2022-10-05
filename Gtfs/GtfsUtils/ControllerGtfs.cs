using NLog;
using SytyRouting.Gtfs.ModelCsv;
using SytyRouting.Gtfs.ModelGtfs;
using NetTopologySuite.Geometries;
using System.Net; //download file 
using System.IO.Compression; //zip
using NetTopologySuite.Operation.Distance;

namespace SytyRouting.Gtfs.GtfsUtils
{
    public class ControllerGtfs
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public ControllerCsv CtrlCsv = new ControllerCsv();
        public ProviderCsv Choice = ProviderCsv.stib;

        public List<StopCsv> RecordsStop { get; }
        public List<RouteCsv> RecordsRoute { get; }
        public List<TripCsv> RecordsTrip { get; }
        public List<ShapeCsv> RecordsShape { get; }
        public List<StopTimesCsv> RecordStopTime { get; }
        public List<CalendarCsv> RecordsCalendar { get; }
        public List<AgencyCsv> RecordsAgency { get; }

        public Dictionary<string, StopGtfs> StopDico { get; }
        public Dictionary<string, RouteGtfs> RouteDico { get; }
        public Dictionary<string, ShapeGtfs> ShapeDico { get; }
        public Dictionary<string, CalendarGtfs> CalendarDico { get; }
        public Dictionary<string, TripGtfs> TripDico { get; }
        public Dictionary<string, AgencyGtfs> AgencyDico { get; }
        public Dictionary<string, ScheduleGtfs> ScheduleDico { get; }
        public Dictionary<string, EdgeGtfs> EdgeDico { get; }


        public ControllerGtfs()
        {
            Task task = DownloadsGtfs();
            Task.WaitAny(task);
            RecordsStop = CtrlCsv.GetAllStops(Choice);
            RecordsRoute = CtrlCsv.GetAllRoutes(Choice);
            RecordsTrip = CtrlCsv.GetAllTrips(Choice);
            RecordsShape = CtrlCsv.GetAllShapes(Choice);
            RecordStopTime = CtrlCsv.GetAllStopTimes(Choice);
            RecordsCalendar = CtrlCsv.GetAllCalendars(Choice);
            RecordsAgency = CtrlCsv.GetAllAgencies(Choice);
            StopDico = CreateStopGtfsDictionary();
            AgencyDico = CreateAgencyGtfsDictionary();
            RouteDico = CreateRouteGtfsDictionary();
            ShapeDico = CreateShapeGtfsDictionary();
            CalendarDico = CreateCalendarGtfsDictionary();
            TripDico = CreateTripGtfsDictionary();
            AddTripsToRoute();
            ScheduleDico = CreateScheduleGtfsDictionary();
            AddScheduleToTrip();
            EdgeDico = AllTripsToEdgeDictionary();
            CleanGtfs();
        }

        private Dictionary<string, AgencyGtfs> CreateAgencyGtfsDictionary()
        {
            var agencyDico = new Dictionary<string, AgencyGtfs>();
            foreach (var agency in RecordsAgency)
            {
                agencyDico.Add(agency.Id, new AgencyGtfs(agency.Id, agency.Name, agency.Url));
            }
            return agencyDico;
        }

        // Creation of dictionaries
        public Dictionary<string, StopGtfs> CreateStopGtfsDictionary()
        {
            var stopDico = new Dictionary<string, StopGtfs>();
            foreach (StopCsv stop in RecordsStop)
            {
                stopDico.Add(stop.Id, new StopGtfs(stop.Id, stop.Name, stop.Lat, stop.Lon));

            }
            return stopDico;
        }

        // Creates the routes but trips are empty
        public Dictionary<string, RouteGtfs> CreateRouteGtfsDictionary()
        {
            var routeDico = new Dictionary<string, RouteGtfs>();
            foreach (var route in RecordsRoute)
            {
                AgencyGtfs buffAgency = null;
                if (route.AgencyId != null)
                {
                    AgencyDico.TryGetValue(route.AgencyId, out buffAgency);
                }
                routeDico.Add(route.Id, new RouteGtfs(route.Id, route.LongName, route.Type, new Dictionary<string, TripGtfs>(), buffAgency));
            }
            return routeDico;
        }

        // Create the shapes
        public Dictionary<string, ShapeGtfs> CreateShapeGtfsDictionary()
        {
            var shapeDico = new Dictionary<string, ShapeGtfs>();
            foreach (var shape in RecordsShape)
            {
                ShapeGtfs shapeBuff = null;
                if (!shapeDico.TryGetValue(shape.Id, out shapeBuff))
                {
                    shapeDico.Add(shape.Id, new ShapeGtfs(shape.Id, new Dictionary<int, Point>(), CreateLineString(shape.Id)));
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

        public Dictionary<string, CalendarGtfs> CreateCalendarGtfsDictionary()
        {
            Dictionary<string, CalendarGtfs> days = new Dictionary<string, CalendarGtfs>();
            CalendarGtfs calBuff = null;
            foreach (var calendar in RecordsCalendar)
            {
                bool[] daysBool = new bool[7];
                if (!days.TryGetValue(calendar.ServiceId, out calBuff))
                {
                    days.Add(calendar.ServiceId, new CalendarGtfs(calendar.ServiceId, daysBool));
                    daysBool[0] = Convert.ToBoolean(calendar.Monday);
                    daysBool[1] = Convert.ToBoolean(calendar.Tuesday);
                    daysBool[2] = Convert.ToBoolean(calendar.Wednesday);
                    daysBool[3] = Convert.ToBoolean(calendar.Thursday);
                    daysBool[4] = Convert.ToBoolean(calendar.Friday);
                    daysBool[5] = Convert.ToBoolean(calendar.Saturday);
                    daysBool[6] = Convert.ToBoolean(calendar.Sunday);
                }
            }
            return days;
        }

        public Dictionary<string, ScheduleGtfs> CreateScheduleGtfsDictionary()
        {
            TripGtfs targetTrip = null;
            ScheduleGtfs schedule = null;
            StopGtfs stopBuff = null;

            // Create the timeStop with an dico details
            var scheduleDico = new Dictionary<string, ScheduleGtfs>();  // String = id of trip 
            foreach (var stopTime in RecordStopTime)
            {
                StopDico.TryGetValue(stopTime.StopId, out stopBuff);
                TimeSpan arrivalTime = ParseMore24Hours(stopTime.ArrivalTime);
                TimeSpan departureTime = ParseMore24Hours(stopTime.DepartureTime);
                StopTimesGtfs newStopTime = new StopTimesGtfs(stopBuff, arrivalTime, departureTime, stopTime.Sequence);
                // If a line already exists
                if (scheduleDico.TryGetValue(stopTime.TripId, out schedule))
                {
                    schedule.Details.Add(stopTime.Sequence, newStopTime);
                }
                else
                {
                    TripDico.TryGetValue(stopTime.TripId, out targetTrip);
                    Dictionary<int, StopTimesGtfs> myDico = new Dictionary<int, StopTimesGtfs>();
                    myDico.Add(stopTime.Sequence, newStopTime);
                    scheduleDico.Add(stopTime.TripId, new ScheduleGtfs(targetTrip.Id, myDico));
                }
            }
            return scheduleDico;
        }

        // Create a trip with a shape (if there's an available shape) and with no schedule
        public Dictionary<string, TripGtfs> CreateTripGtfsDictionary()
        {
            var tripDico = new Dictionary<string, TripGtfs>();
            RouteGtfs buffRoute = null;
            ShapeGtfs buffShape = null;
            CalendarGtfs buffCal = null;
            foreach (TripCsv trip in RecordsTrip)
            {
                if (RouteDico.TryGetValue(trip.RouteId, out buffRoute))
                {
                    CalendarDico.TryGetValue(trip.ServiceId, out buffCal);
                    TripGtfs newTrip;
                    if (trip.ShapeId != null && ShapeDico.TryGetValue(trip.ShapeId, out buffShape))
                    {
                        newTrip = new TripGtfs(buffRoute, trip.Id, buffShape, null, buffCal);
                    }
                    else
                    {
                        newTrip = new TripGtfs(buffRoute, trip.Id, null, null, buffCal);
                    }
                    tripDico.Add(trip.Id, newTrip);
                }
            }
            return tripDico;
        }

        public Dictionary<string, EdgeGtfs> AllTripsToEdgeDictionary()
        {
            var edgeDico = new Dictionary<string, EdgeGtfs>();
            var edgeDicoOneTrip = new Dictionary<string, EdgeGtfs>();
            foreach (var trip in TripDico)
            {
                edgeDicoOneTrip = OneTripToEdgeDictionary(trip.Key);
                foreach (var edge in edgeDicoOneTrip)
                {
                    edgeDico.TryAdd(edge.Key, edge.Value);
                }
            }
            return edgeDico;
        }

        public Dictionary<string, EdgeGtfs> OneTripToEdgeDictionary(string tripId)
        {
            Dictionary<string, EdgeGtfs> edgeDico = new Dictionary<string, EdgeGtfs>();
            TripGtfs buffTrip = null;
            if (TripDico.TryGetValue(tripId, out buffTrip))
            {
                StopGtfs currentStop = null;
                StopGtfs previousStop = null;
                StopTimesGtfs previousStopTime = null;
                foreach (var currentStopTime in buffTrip.Schedule.Details)
                {
                    currentStop = currentStopTime.Value.Stop;
                    if (previousStop != null)
                    {
                        string newId = previousStop.Id + currentStop.Id + buffTrip.Route.Type;
                        double distance = Helper.GetDistance(previousStop.Lon, previousStop.Lat, currentStop.Lon, currentStop.Lat);
                        TimeSpan arrival = currentStopTime.Value.ArrivalTime;
                        TimeSpan departure = previousStopTime.DepartureTime;
                        double duration = (arrival - departure).TotalSeconds;
                        ShapeGtfs shape = buffTrip.Shape;
                        bool iShapeAvailable;
                        iShapeAvailable = shape == null ? false : true;
                        EdgeGtfs newEdge = null;
                        if (iShapeAvailable)
                        {
                            Point sourceNearestLineString = new Point(DistanceOp.NearestPoints(shape.LineString, new Point(previousStop.Lat, previousStop.Lon))[0]);
                            Point targetNearestLineString = new Point(DistanceOp.NearestPoints(shape.LineString, new Point(currentStop.Lat, currentStop.Lon))[0]);
                            double walkDistanceSourceM = Helper.GetDistance(sourceNearestLineString.X, sourceNearestLineString.Y, previousStop.Lat, previousStop.Lon);
                            double walkDistanceTargetM = Helper.GetDistance(targetNearestLineString.X, targetNearestLineString.Y, currentStop.Lat, currentStop.Lon);
                            double distanceNearestPointsM = Helper.GetDistance(sourceNearestLineString.X, sourceNearestLineString.Y, targetNearestLineString.X, targetNearestLineString.Y);
                            newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, iShapeAvailable, sourceNearestLineString, targetNearestLineString, walkDistanceSourceM, walkDistanceTargetM, distanceNearestPointsM, distanceNearestPointsM / duration);
                            edgeDico.Add(newId, newEdge);
                        }
                        else
                        {
                            newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, iShapeAvailable, null, null, 0, 0, 0, distance / duration);
                            edgeDico.Add(newId, newEdge);
                        }
                        previousStop.ValidSource = true;
                        currentStop.ValidTarget = true;
                        previousStop.OutwardEdges.Add(newEdge);
                        currentStop.InwardEdges.Add(newEdge);
                    }
                    previousStop = currentStop;
                    previousStopTime = currentStopTime.Value;
                }
            }
            return edgeDico;
        }

        public void AddTripsToRoute()
        {
            foreach (KeyValuePair<string, TripGtfs> trip in TripDico)
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

        public void AddScheduleToTrip()
        {
            TripGtfs tripBuff = null;
            foreach (var schedule in ScheduleDico)
            {
                if (TripDico.TryGetValue(schedule.Key, out tripBuff))
                {
                    tripBuff.Schedule = schedule.Value;
                }
            }
        }

        public Dictionary<string, TripGtfs> SelectAllTripsForGivenDay(int day)
        {
            Dictionary<string, TripGtfs> targetedTrips = new Dictionary<string, TripGtfs>();
            var query = from trip in TripDico
                        where trip.Value.Service.Days[day] == true
                        select trip;
            return query.ToDictionary(k => k.Key, v => v.Value);
        }

        public Dictionary<string, TripGtfs> SelectAllTripsForMondayBetween10and11(Dictionary<string, TripGtfs> tripDico, Dictionary<string, ScheduleGtfs> scheduleDico)
        {
            return SelectAllTripsForGivenDayAndBetweenGivenHours(new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0), 6);
        }

        /**
         0 for monday, 1 for tuesday, 2 for wednesday, 3 for thursday, 4 for friday, 5 for saturday, 6 for sunday
        */
        public Dictionary<string, TripGtfs> SelectAllTripsForGivenDayAndBetweenGivenHours(TimeSpan min, TimeSpan max, int day)
        {
            Dictionary<string, TripGtfs> targetedTrips = new Dictionary<string, TripGtfs>();
            var tripsForOneDay = SelectAllTripsForGivenDay(day);
            var tripsForOneDayBetweenHours = from trip in tripsForOneDay
                                             where trip.Value.Schedule.Details.First().Value.DepartureTime >= min && trip.Value.Schedule.Details.First().Value.DepartureTime <= max
                                             select trip;
            return tripsForOneDayBetweenHours.ToDictionary(k => k.Key, v => v.Value);
        }

        public LineString CreateLineString(string shapeId)
        {
            var shapeInfos = RecordsShape.FindAll(x => x.Id == shapeId);
            // CREATION of LINESTRING
            Coordinate[] arrayOfCoordinate = new Coordinate[shapeInfos.Count];
            for (int i = 0; i < shapeInfos.Count; i++)
            {
                ShapeCsv shape = shapeInfos[i];
                Coordinate coordinate = new Coordinate(shape.PtLat, shape.PtLon);
                arrayOfCoordinate[i] = coordinate;
            }
            LineString lineString = new LineString(arrayOfCoordinate);
            return lineString;
        }

        public TimeSpan ParseMore24Hours(string timeToParse)
        {
            string[] split = timeToParse.Split(":");
            int hour = Int16.Parse(split[0]);
            int min = Int16.Parse(split[1]);
            int seconds = Int16.Parse(split[2]);
            return new TimeSpan(hour % 24, min, seconds);
        }

        ///////////////////
        public static async Task DownloadsGtfs()
        {
            CleanGtfs();
            List<Task> listDwnld = new List<Task>();
            foreach (ProviderCsv provider in Enum.GetValues(typeof(ProviderCsv)))
            {
                listDwnld.Add(DownloadGtfs(provider));
            }
            if (listDwnld.Count == 0)
            {
                logger.Info("Nothing to download");
            }
            try
            {
                await Task.WhenAll(listDwnld);
            }
            catch (AggregateException e)
            {
                var collectedExceptions = e.InnerExceptions;
                logger.Info("Error with the download of {0} provider(s)", collectedExceptions.Count);
                foreach (var inEx in collectedExceptions)
                {
                    logger.Info(inEx.Message);
                }
            }
            catch (Exception)
            {
                // Case when there is no provider.
            }
        }

        public static void CleanGtfs()
        {
            if (Directory.Exists("GtfsData"))
            {
                Directory.Delete("GtfsData", true);
                logger.Info("Cleaning GtfsData");
            }
            else
            {
                logger.Info("No data found");
            }
        }

        private static async Task DownloadGtfs(ProviderCsv provider)
        {
            string path = System.IO.Path.GetFullPath("GtfsData");

            logger.Info("Start download {0}", provider);
            string fullPathDwln = path + $"\\{provider}\\gtfs.zip";
            string fullPathExtract = path + $"\\{provider}\\gtfs";
            Uri linkOfGtfs = new Uri("https://huhu");
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(path + $"\\{provider}");
            switch (provider)
            {
                case ProviderCsv.stib:
                    linkOfGtfs = new Uri("https://stibmivb.opendatasoft.com/api/datasets/1.0/gtfs-files-production/alternative_exports/gtfszip/");
                    break;
                case ProviderCsv.tec:
                    linkOfGtfs = new Uri("https://gtfs.irail.be/tec/tec-gtfs.zip");
                    break;
                case ProviderCsv.ter:
                    linkOfGtfs = new Uri("https://eu.ftp.opendatasoft.com/sncf/gtfs/export-ter-gtfs-last.zip");
                    break;
                case ProviderCsv.tgv:
                    linkOfGtfs = new Uri("https://eu.ftp.opendatasoft.com/sncf/gtfs/export_gtfs_voyages.zip");
                    break;
                case ProviderCsv.canada:
                    linkOfGtfs = new Uri("https://transitfeeds.com/p/calgary-transit/238/latest/download");
                    break;
            }
            Task dwnldAsync;

            using (WebClient wc = new WebClient())
            {
                dwnldAsync = wc.DownloadFileTaskAsync(
                    // Param1 = Link of file
                    linkOfGtfs,
                    // Param2 = Path to save
                    fullPathDwln);
                logger.Info("downloaded directory for {0}", provider);
            }
            try
            {
                await dwnldAsync;
            }
            catch
            {
                logger.Info("Error with the provider {0}", provider);
                throw;
            }
            await Task.Run(() => ZipFile.ExtractToDirectory(fullPathDwln, fullPathExtract));
            logger.Info("{0} done", provider);

            if (Directory.Exists(fullPathExtract))
            {
                File.Delete(fullPathDwln); //delete .zip
            }
        }
    }
}