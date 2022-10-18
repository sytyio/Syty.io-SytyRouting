using NLog;
using SytyRouting.Gtfs.ModelCsv;
using System.Diagnostics;

using SytyRouting.Model;
using SytyRouting.Gtfs.ModelGtfs;
using NetTopologySuite.Geometries;
using System.Net; //download file 
using System.IO.Compression; //zip
using NetTopologySuite.Operation.Distance;
using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.LinearReferencing;
namespace SytyRouting.Gtfs.GtfsUtils
{
    public class ControllerGtfs
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [NotNull]
        public ControllerCsv? CtrlCsv;
        private ProviderCsv choice;

        private static int idGeneratorAgency = int.MaxValue - 10000;

        [NotNull]
        public Dictionary<string, StopGtfs>? StopDico;
        [NotNull]
        public Dictionary<string, RouteGtfs>? RouteDico;
        [NotNull]
        public Dictionary<string, ShapeGtfs>? ShapeDico;
        [NotNull]
        public Dictionary<string, CalendarGtfs>? CalendarDico;
        [NotNull]
        public Dictionary<string, TripGtfs>? TripDico;
        [NotNull]
        public Dictionary<string, AgencyGtfs>? AgencyDico;
        [NotNull]
        public Dictionary<string, ScheduleGtfs>? ScheduleDico;
        [NotNull]
        public Dictionary<string, EdgeGtfs>? EdgeDico;



        public ControllerGtfs(ProviderCsv provider)
        {
            choice = provider;
        }

        public async Task InitController()
        {

            // await DownloadsGtfs();
            CtrlCsv = new ControllerCsv(choice);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            StopDico = CreateStopGtfsDictionary();
            logger.Info("Stop dico nb stops = {0} for {1} in {2}", StopDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AgencyDico = CreateAgencyGtfsDictionary();
            logger.Info("Agency nb {0} for {1} in {2}", AgencyDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            RouteDico = CreateRouteGtfsDictionary();
            logger.Info("Route nb {0} for {1} in {2}", RouteDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            ShapeDico = CreateShapeGtfsDictionary();
            logger.Info("Shape nb {0} for {1} in {2}", ShapeDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            CalendarDico = CreateCalendarGtfsDictionary();
            logger.Info("Calendar nb {0} for {1} in {2}", CalendarDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            ScheduleDico = CreateScheduleGtfsDictionary();
            logger.Info("Schedule nb {0} for {1} in {2}", ScheduleDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            TripDico = CreateTripGtfsDictionary();
            logger.Info("Trip  nb {0} for {1} in {2}", TripDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AddTripsToRoute();
            logger.Info("Trip to route for {0} in {1}", choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AddSplitLineString();
            logger.Info("Add split linestring loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            EdgeDico = AllTripsToEdgeDictionary();
            logger.Info("Edge  dico loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Stop();
            // CleanGtfs();
        }



        private Dictionary<string, AgencyGtfs> CreateAgencyGtfsDictionary()
        {
            if (CtrlCsv.RecordsAgency.Count == 1 && CtrlCsv.RecordsAgency[0].Id == null)
            {
                var newId = idGeneratorAgency.ToString();
                idGeneratorAgency++;
                return CtrlCsv.RecordsAgency.ToDictionary(k => newId, k => new AgencyGtfs(newId, k.Name, k.Url));
                /** Agency id isn't a required field. 
                 If there is less than one agency per supplier, may not be given
                if there is no id provided but there is an agency, creation of an id */
            }
            return CtrlCsv.RecordsAgency.ToDictionary(k => k.Id, k => new AgencyGtfs(k.Id, k.Name, k.Url));
        }

        // Creation of dictionaries
        private Dictionary<string, StopGtfs> CreateStopGtfsDictionary()
        {
            return CtrlCsv.RecordsStop.ToDictionary(stop => stop.Id, stop => new StopGtfs(stop.Id, stop.Name, stop.Lat, stop.Lon));
        }

        // Creates the routes but trips are empty
        private Dictionary<string, RouteGtfs> CreateRouteGtfsDictionary()
        {
            if (CtrlCsv.RecordsAgency.Count == 0)
            {
                return CtrlCsv.RecordsRoute.ToDictionary(x => x.Id, x => new RouteGtfs(x.Id, x.LongName, x.Type, new Dictionary<string, TripGtfs>(), null));
            }
            return CtrlCsv.RecordsRoute.ToDictionary(x => x.Id, x => new RouteGtfs(x.Id, x.LongName, x.Type, new Dictionary<string, TripGtfs>(), GetAgencyOrNull(x.AgencyId)));
        }

        private AgencyGtfs? GetAgencyOrNull(string id)
        {
            if (id == null)
            {
                var test = AgencyDico.First().Key;
                return test == null ? null : AgencyDico[test];
            }
            return AgencyDico[id];
        }

        // Create the shapes
        private Dictionary<string, ShapeGtfs> CreateShapeGtfsDictionary()
        {
            return CtrlCsv.RecordsShape.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => new ShapeGtfs(x.Key, x.OrderBy(y => y.PtSequence).ToDictionary(y => y.PtSequence, y => (new Point(y.PtLon, y.PtLat))), CreateLineString(x.Key)));
        }

        private Dictionary<string, CalendarGtfs> CreateCalendarGtfsDictionary()
        {
            return CtrlCsv.RecordsCalendar.ToDictionary(x => x.ServiceId, x => new CalendarGtfs(x.ServiceId, new bool[]{
                                                                    Convert.ToBoolean(x.Monday),
                                                                    Convert.ToBoolean(x.Tuesday),
                                                                    Convert.ToBoolean(x.Wednesday),
                                                                    Convert.ToBoolean(x.Thursday),
                                                                    Convert.ToBoolean(x.Friday),
                                                                    Convert.ToBoolean(x.Saturday),
                                                                    Convert.ToBoolean(x.Sunday)}));
        }

        private Dictionary<string, ScheduleGtfs> CreateScheduleGtfsDictionary()
        {
            return CtrlCsv.RecordStopTime.GroupBy(x => x.TripId).ToDictionary(x => x.Key, x => new ScheduleGtfs(x.Key, x.ToDictionary(y => y.Sequence,
                               y => (new StopTimesGtfs(StopDico[y.StopId], ParseMore24Hours(y.ArrivalTime), ParseMore24Hours(y.DepartureTime), y.Sequence)))));
        }

        // Create a trip with a shape (if there's an available shape) and with no schedule
        private Dictionary<string, TripGtfs> CreateTripGtfsDictionary()
        {

            var tripDico = new Dictionary<string, TripGtfs>();
            if (ShapeDico.Count == 0)
            {
                return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(RouteDico[x.RouteId], x.Id, null, ScheduleDico[x.Id], CalendarDico[x.ServiceId]));
            }
            return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(RouteDico[x.RouteId], x.Id, ShapeDico[x.ShapeId!], ScheduleDico[x.Id], CalendarDico[x.ServiceId]));
        }

        private Dictionary<string, EdgeGtfs> AllTripsToEdgeDictionary()
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

        private void AddSplitLineString()
        {
            foreach (var trip in TripDico)
            {
                if (trip.Value.Shape != null)
                {
                    var stopsCoordinatesArray = new Point[trip.Value.Schedule.Details.Count()];
                    int i = 0;
                    foreach (var stopTimes in trip.Value.Schedule.Details)
                    {
                        stopsCoordinatesArray[i] = new Point(stopTimes.Value.Stop.Y, stopTimes.Value.Stop.X);
                        i++;
                    }
                    var currentShape = trip.Value.Shape;
                    currentShape.SplitLineString = SplitLineStringByPoints(currentShape.LineString, stopsCoordinatesArray, currentShape.Id);
                }
            }

        }

        private Dictionary<string, EdgeGtfs> OneTripToEdgeDictionary(string tripId)
        {
            Dictionary<string, EdgeGtfs> edgeDico = new Dictionary<string, EdgeGtfs>();
            TripGtfs buffTrip = TripDico[tripId];
            ShapeGtfs? buffShape = buffTrip.Shape;
            StopGtfs currentStop;
            StopGtfs? previousStop = null;
            StopTimesGtfs? previousStopTime = null;
            if (buffShape != null)
            {
                buffShape.ArrayDistances = new double[buffTrip.Schedule.Details.Count()];
            }
            int i = -1;
            foreach (var currentStopTime in buffTrip.Schedule.Details)
            {
                currentStop = currentStopTime.Value.Stop;
                if (previousStop != null && previousStopTime != null)
                {
                    string newId = currentStop.Id + "TO" + previousStop.Id + "IN" + buffTrip.Route.Id;
                    if (!edgeDico.ContainsKey(newId))
                    {
                        double distance = Helper.GetDistance(previousStop.X, previousStop.Y, currentStop.X, currentStop.Y);
                        TimeSpan arrival = currentStopTime.Value.ArrivalTime;
                        TimeSpan departure = previousStopTime.DepartureTime;
                        double duration = (arrival - departure).TotalSeconds;
                        EdgeGtfs newEdge;
                        if (buffShape != null)
                        {
                            Point sourceNearestLineString = new Point(DistanceOp.NearestPoints(buffShape.LineString, new Point(previousStop.Y, previousStop.X))[0]);
                            Point targetNearestLineString = new Point(DistanceOp.NearestPoints(buffShape.LineString, new Point(currentStop.Y, currentStop.X))[0]);
                            double walkDistanceSourceM = Helper.GetDistance(sourceNearestLineString.X, sourceNearestLineString.Y, previousStop.Y, previousStop.X);
                            double walkDistanceTargetM = Helper.GetDistance(targetNearestLineString.X, targetNearestLineString.Y, currentStop.Y, currentStop.X);
                            double distanceNearestPointsM = Helper.GetDistance(sourceNearestLineString.X, sourceNearestLineString.Y, targetNearestLineString.X, targetNearestLineString.Y);
                            LineString lineString = buffShape.LineString;
                            LineString? splitLineString = null;
                            if(i>=0){
                                splitLineString = buffTrip.Shape.SplitLineString[i];
                            }
                            if (splitLineString == null)
                            {
                                newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, false, null, null, 0, 0, 0, distance / duration, null);
                            }
                            else
                            {
                                var internalGeom = Helper.GetInternalGeometry(splitLineString, OneWayState.Yes);
                                newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, true, sourceNearestLineString, targetNearestLineString, walkDistanceSourceM,
                                            walkDistanceTargetM, distanceNearestPointsM, distanceNearestPointsM / duration, internalGeom);
                                i++;
                            }
                        }
                        else
                        {
                            newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, false, null, null, 0, 0, 0, distance / duration, null);
                            edgeDico.Add(newId, newEdge);
                        }
                        previousStop.ValidSource = true;
                        currentStop.ValidTarget = true;
                        if (!previousStop.OutwardEdgesGtfs.ContainsKey(newEdge.Id))
                        {

                            previousStop.OutwardEdgesGtfs.Add(newEdge.Id, newEdge);
                            previousStop.OutwardEdges.Add(newEdge);
                        }
                        if (!currentStop.InwardEdgesGtfs.ContainsKey(newEdge.Id))
                        {

                            currentStop.InwardEdgesGtfs.Add(newEdge.Id, newEdge);
                            currentStop.InwardEdges.Add(newEdge);
                        }
                    }
                }
                previousStop = currentStop;
                previousStopTime = currentStopTime.Value;
            }
            return edgeDico;
        }

        private List<LineString> SplitLineStringByPoints(LineString ls, Point[] pts, string shapeId)
        {
            LengthLocationMap llm = new LengthLocationMap(ls);
            LengthIndexedLine lil = new LengthIndexedLine(ls);
            ExtractLineByLocation ell = new ExtractLineByLocation(ls);
            List<LineString> parts = new List<LineString>();
            LinearLocation? ll1 = null;
            SortedList<Double, LinearLocation> sll = new SortedList<double, LinearLocation>();
            sll.Add(-1, new LinearLocation(0, 0d));
            foreach (Point pt in pts)
            {
                Double distanceOnLinearString = lil.Project(pt.Coordinate);
                if (sll.ContainsKey(distanceOnLinearString)==false){
                    sll.Add(distanceOnLinearString, llm.GetLocation(distanceOnLinearString));
                }
            }
            foreach (LinearLocation ll in sll.Values)
            {
                if (ll1 != null)
                {
                    parts.Add((LineString)ell.Extract(ll1, ll));
                }
                ll1 = ll;
            }
            return parts;
        }

        private void AddTripsToRoute()
        {
            foreach (KeyValuePair<string, TripGtfs> trip in TripDico)
            {
                trip.Value.Route.Trips.Add(trip.Key, trip.Value);
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

        public int GetNumberStops()
        {
            return StopDico.Count();
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

        private LineString CreateLineString(string shapeId)
        {
            var shapeInfos = CtrlCsv.RecordsShape.FindAll(x => x.Id == shapeId);
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

        private TimeSpan ParseMore24Hours(string timeToParse)
        {
            string[] split = timeToParse.Split(":");
            int hour = Int16.Parse(split[0]);
            int min = Int16.Parse(split[1]);
            int seconds = Int16.Parse(split[2]);
            return new TimeSpan(hour % 24, min, seconds);
        }

        private static async Task DownloadsGtfs()
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

        private static void CleanGtfs()
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