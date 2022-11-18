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

    public class ControllerGtfs : ControllerExternalSource
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [NotNull]
        public ControllerCsv? CtrlCsv;
        public string choice;

        private static int idGeneratorAgency = int.MaxValue - 10000;

        [NotNull]
        private Dictionary<string, StopGtfs>? stopDico;

        [NotNull]
        private Dictionary<string, RouteGtfs>? routeDico;
        [NotNull]
        private Dictionary<string, ShapeGtfs>? shapeDico;
        [NotNull]
        private Dictionary<string, CalendarGtfs>? calendarDico;
        [NotNull]
        private Dictionary<string, TripGtfs>? tripDico;
        [NotNull]
        private Dictionary<string, AgencyGtfs>? agencyDico;
        [NotNull]
        private Dictionary<string, ScheduleGtfs>? scheduleDico;
        [NotNull]
        private Dictionary<string, Edge>? edgeDico = new Dictionary<string, Edge>();

        // Nearest nodes
        private Dictionary<string, Node> nearestNodeDico = new Dictionary<string, Node>();

        //Masks
        private Dictionary<int, byte> routeTypeToTransportMode = new Dictionary<int, byte>();        
        private Dictionary<int, byte> transportModeMasks = new Dictionary<int, byte>();


        public ControllerGtfs(string provider)
        {
            choice = provider;
        }

        public async Task InitController()
        {
            // await DownloadGtfs();
            CtrlCsv = new ControllerCsv(choice);

            transportModeMasks = TransportModes.TransportModeMasks;
            routeTypeToTransportMode = TransportModes.CreateMappingRouteTypeToTransportMode(transportModeMasks);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            stopDico = CreateStopGtfsDictionary();
            logger.Info("Stop dico nb stops = {0} for {1} in {2}", stopDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            agencyDico = CreateAgencyGtfsDictionary();
            logger.Info("Agency nb {0} for {1} in {2}", agencyDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            routeDico = CreateRouteGtfsDictionary();
            logger.Info("Route nb {0} for {1} in {2}", routeDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            shapeDico = CreateShapeGtfsDictionary();
            logger.Info("Shape nb {0} for {1} in {2}", shapeDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            calendarDico = CreateCalendarGtfsDictionary();
            logger.Info("Calendar nb {0} for {1} in {2}", calendarDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            scheduleDico = CreateScheduleGtfsDictionary();
            logger.Info("Schedule nb {0} for {1} in {2}", scheduleDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            tripDico = CreateTripGtfsDictionary();
            logger.Info("Trip  nb {0} for {1} in {2}", tripDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AddTripsToRoute();
            logger.Info("Trip to route for {0} in {1}", choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AddSplitLineString();
            logger.Info("Add split linestring loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Restart();
            AllTripsToEdgeDictionary();
            logger.Info("Edge  dico loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));
            stopWatch.Stop();
        }

        public IEnumerable<Node> GetNodes()
        {
            return stopDico.Values.Cast<Node>();
        }

        public IEnumerable<Node> GetInternalNodes()
        {
            return nearestNodeDico.Values;
        }

        public IEnumerable<Edge> GetEdges()
        {
            return edgeDico.Values.Cast<Edge>();
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
            return CtrlCsv.RecordsRoute.ToDictionary(x => x.Id, x => new RouteGtfs(x.Id, x.LongName, x.Type, new Dictionary<string, TripGtfs>(), GetAgency(x.AgencyId)));
        }

        private AgencyGtfs? GetAgency(string id)
        {
            if (id == null)
            {
                return agencyDico.First().Value;
            }
            return agencyDico[id];
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
                               y => (new StopTimesGtfs(stopDico[y.StopId], ParseMore24Hours(y.ArrivalTime), ParseMore24Hours(y.DepartureTime), y.Sequence)))));
        }

        // Create a trip with a shape (if there's an available shape) and with no schedule
        private Dictionary<string, TripGtfs> CreateTripGtfsDictionary()
        {

            var tripDico = new Dictionary<string, TripGtfs>();
            if (shapeDico.Count == 0)
            {
                return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, null, scheduleDico[x.Id], calendarDico[x.ServiceId]));
            }
            return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, shapeDico[x.ShapeId!], scheduleDico[x.Id], calendarDico[x.ServiceId]));
        }

        private void AllTripsToEdgeDictionary()
        {
            foreach (var trip in tripDico)
            {
                OneTripToEdgeDictionary(trip.Key);
            }
        }

        private void AddSplitLineString()
        {
            foreach (var trip in tripDico)
            {
                if (trip.Value.Shape != null)
                {
                    var stopsCoordinatesArray = new Point[trip.Value.Schedule.Details.Count()];
                    int i = 0;
                    foreach (var stopTimes in trip.Value.Schedule.Details)
                    {
                        stopsCoordinatesArray[i] = new Point(stopTimes.Value.Stop.X, stopTimes.Value.Stop.Y);
                        i++;
                    }
                    var currentShape = trip.Value.Shape;
                    currentShape.SplitLineString = SplitLineStringByPoints(currentShape.LineString, stopsCoordinatesArray, currentShape.Id);
                }
            }
        }

        private void OneTripToEdgeDictionary(string tripId)
        {
            TripGtfs buffTrip = tripDico[tripId];
            ShapeGtfs? buffShape = buffTrip.Shape;
            StopGtfs? previousStop = null;
            StopTimesGtfs? previousStopTime = null;
            Node? previousNearestOnLineString = null;

            if (buffShape != null)
            {
                buffShape.ArrayDistances = new double[buffTrip.Schedule.Details.Count()];
            }
            int i = 1;
            int cpt = 0;
            foreach (var currentStopTime in buffTrip.Schedule.Details)
            {
                var currentStop = currentStopTime.Value.Stop;
                Node currentNearestNodeOnLineString = new Node { Idx = 0, OsmID = long.MaxValue };
                // var specificTransportMode = TransportModes.GetTransportModeMask(FromRouteTypeToTransportName(buffTrip.Route.Type));
                // logger.Info("Maaaask {0}, string {1}",specificTransportMode,FromRouteTypeToTransportName(buffTrip.Route.Type));
                if (previousStop != null && previousStopTime != null && previousNearestOnLineString != null)
                {
                    currentStop.ValidSource = true;
                    string newId = previousStop.Id + "TO" + currentStop.Id + "IN" + buffTrip.Route.Id;
                    if (!edgeDico.ContainsKey(newId))
                    {
                        double distance = Helper.GetDistance(previousStop.X, previousStop.Y, currentStop.X, currentStop.Y); // Replace by distance with de splitLineString 
                        TimeSpan arrival = currentStopTime.Value.ArrivalTime;
                        TimeSpan departure = previousStopTime.DepartureTime;

                        // temporary solution to include waiting time (later: use of a penalty)
                        var watchTime = (previousStopTime.DepartureTime - previousStopTime.ArrivalTime).TotalSeconds;
                        var duration = (arrival - departure).TotalSeconds + watchTime;

                        EdgeGtfs newEdge;
                        // var routeType = buffTrip.Route.Type;

                        if (buffShape != null) // if there is a linestring, the edge will be created between the two nearest points of the stops on the linestring
                        {
                            Point sourceNearestLineString = new Point(DistanceOp.NearestPoints(buffShape.LineString, new Point(previousStop.X, previousStop.Y))[0]);
                            Point targetNearestLineString = new Point(DistanceOp.NearestPoints(buffShape.LineString, new Point(currentStop.X, currentStop.Y))[0]);

                            /// Add the nearest node
                            // Distance between current and nearest on lineString
                            var length = Helper.GetDistance(targetNearestLineString.X, targetNearestLineString.Y, currentStop.X, currentStop.Y);
                            currentNearestNodeOnLineString.X = targetNearestLineString.X;
                            currentNearestNodeOnLineString.Y = targetNearestLineString.Y;

                            // Add to the dictionary
                            var idForNearestNode = newId + "N";
                            if (!nearestNodeDico.ContainsKey(idForNearestNode))
                            {
                                AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, length,cpt, sourceNearestLineString);
                            }
                            LineString lineString = buffShape.LineString;
                            LineString splitLineString = buffShape.SplitLineString[i];
                            var internalGeom = Helper.GetInternalGeometry(splitLineString, OneWayState.Yes);

                                if (AreNodesAtSamePosition(previousStop, previousNearestOnLineString) && AreNodesAtSamePosition(currentStop, currentNearestNodeOnLineString))
                                {
                                    distance = GetDistanceWithLineString(splitLineString, previousStop, currentStop);
                                    newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, true, previousStop, currentStop,
                                              distance / duration, internalGeom, TransportModes.PublicTransportModes);
                                    AddEdgeToNodesLineString(previousStop, currentStop, newEdge);
                                }
                                else if (AreNodesAtSamePosition(previousStop, previousNearestOnLineString))
                                {
                                    distance = GetDistanceWithLineString(splitLineString, previousStop, currentNearestNodeOnLineString);
                                    newEdge = new EdgeGtfs(newId, previousStop, currentNearestNodeOnLineString, distance, duration, buffTrip.Route, true, previousStop, currentStop,
                                              distance / duration, internalGeom, TransportModes.PublicTransportModes);
                                    AddEdgeToNodesLineString(previousStop, currentNearestNodeOnLineString, newEdge);
                                }
                                else if (AreNodesAtSamePosition(currentNearestNodeOnLineString, currentStop))
                                {
                                    distance = GetDistanceWithLineString(splitLineString, previousNearestOnLineString, currentStop);
                                    newEdge = new EdgeGtfs(newId, previousNearestOnLineString, currentStop, distance, duration, buffTrip.Route, true, previousStop, currentStop,
                                              distance / duration, internalGeom, TransportModes.PublicTransportModes);
                                    AddEdgeToNodesLineString(previousNearestOnLineString, currentStop, newEdge);
                                }
                                else
                                {
                                    distance = GetDistanceWithLineString(splitLineString, previousNearestOnLineString, currentNearestNodeOnLineString);
                                    newEdge = new EdgeGtfs(newId, previousNearestOnLineString, currentNearestNodeOnLineString, distance, duration, buffTrip.Route, true, previousStop, currentStop,
                                              distance / duration, internalGeom, TransportModes.PublicTransportModes);
                                    AddEdgeToNodesLineString(previousNearestOnLineString, currentNearestNodeOnLineString, newEdge);
                                }

                                edgeDico.Add(newId, newEdge);
                                i++;
                        }
                        else // if there is no linestring
                        {
                            newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, false, null, null, distance / duration, null, TransportModes.PublicTransportModes);
                            edgeDico.Add(newId, newEdge);
                            AddEdgeToNodesNoLineString(previousStop, currentStop, newEdge);
                        }
                    }
                }
                else
                { // For the first stop of a trip 
                    if (buffShape != null)
                    {
                        var idForNearestNode = currentStop.Id + "IN" + buffTrip.Route.Id + "N";
                        if (!nearestNodeDico.ContainsKey(idForNearestNode))
                        {
                            Point sourceNearestLineString = new Point(DistanceOp.NearestPoints(buffShape.LineString, new Point(currentStop.X, currentStop.Y))[0]);
                            currentNearestNodeOnLineString.X = sourceNearestLineString.X;
                            currentNearestNodeOnLineString.Y = sourceNearestLineString.Y;
                            var length = Helper.GetDistance(sourceNearestLineString.X, sourceNearestLineString.Y, currentStop.X, currentStop.Y);
                            AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, length,cpt, sourceNearestLineString);
                        }
                    }
                }
                previousStop = currentStop;
                if (cpt != buffTrip.Schedule.Details.Count - 1)
                {
                    previousStop.ValidTarget = true;
                }
                cpt++;
                previousStopTime = currentStopTime.Value;
                previousNearestOnLineString = currentNearestNodeOnLineString;
            }
        }

        private Boolean AreNodesAtSamePosition(Node a, Node b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        private void AddEdgeToNodesNoLineString(StopGtfs previousStop, StopGtfs currentStop, EdgeGtfs newEdge)
        {
            
            
            if (!previousStop.OutwardEdgesGtfs.ContainsKey(newEdge.Id))
            {
                previousStop.OutwardEdgesGtfs.Add(newEdge.Id, newEdge);
                previousStop.OutwardEdges.Add((Edge)newEdge);
            }
            if (!currentStop.InwardEdgesGtfs.ContainsKey(newEdge.Id))
            {
                currentStop.InwardEdgesGtfs.Add(newEdge.Id, newEdge);
                currentStop.InwardEdges.Add((Edge)newEdge);
            }
        }

        private void AddEdgeToNodesLineString(Node previousStop, Node currentStop, EdgeGtfs newEdge)
        {
            previousStop.OutwardEdges.Add((Edge)newEdge);
            currentStop.InwardEdges.Add((Edge)newEdge);
        }

        private void AddNearestNodeCreateEdges(StopGtfs currentStop, Node currentNearestNodeOnLineString, string id, TripGtfs buffTrip, double distance, int cpt, Point sourceNearestLineString)
        {

            if (!(currentStop.X == currentNearestNodeOnLineString.X && currentStop.Y == currentNearestNodeOnLineString.Y))
            {
                logger.Debug("Stop and nearest node are not at the same coordinates. Stop = {0} {1}, Nearest = {2} {3}", currentStop.Y, currentStop.X, currentNearestNodeOnLineString.Y, currentNearestNodeOnLineString.X);

                nearestNodeDico.Add(id, currentNearestNodeOnLineString);
                // The edges from stop to nearest node and back
                //Temporary : use of Bicyclette type for the walk between de stop and the nearest point on the linestring
                var edgeWalkStopToNearest = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.GetTransportModeMask("Foot"), SourceNode = currentStop, TargetNode = currentNearestNodeOnLineString };
                var edgeWalkNearestToStop = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.GetTransportModeMask("Foot"), SourceNode = currentNearestNodeOnLineString, TargetNode = currentStop };
                // Add the edges to the nodes
                currentNearestNodeOnLineString.OutwardEdges.Add(edgeWalkNearestToStop);
                currentStop.InwardEdges.Add(edgeWalkNearestToStop);
                if(cpt!=buffTrip.Schedule.Details.Count-1){
                    currentStop.OutwardEdges.Add(edgeWalkStopToNearest);
                    currentNearestNodeOnLineString.InwardEdges.Add(edgeWalkStopToNearest);
                }
            }
            else
            {
                logger.Debug("Stop and nearest node are at the same coordinates. Stop = {0} {1}, Nearest = {2} {3}", currentStop.Y, currentStop.X, currentNearestNodeOnLineString.Y, currentNearestNodeOnLineString.X);
            }
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
                if (sll.ContainsKey(distanceOnLinearString) == false)
                {
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

        private string FromRouteTypeToTransportName(int routeType){
            switch(routeType){
                case 0:
                    return "Tram";
                case 1:
                    return "Metro";
                case 2:
                    return "Train";
                case 3:
                    return "Bus";
                default:
                return "None";
            }
        }

        private void AddTripsToRoute()
        {
            foreach (KeyValuePair<string, TripGtfs> trip in tripDico)
            {
                trip.Value.Route.Trips.Add(trip.Key, trip.Value);
            }
        }

        public Dictionary<string, TripGtfs> SelectAllTripsForGivenDay(int day)
        {
            Dictionary<string, TripGtfs> targetedTrips = new Dictionary<string, TripGtfs>();
            var query = from trip in tripDico
                        where trip.Value.Service.Days[day] == true
                        select trip;
            return query.ToDictionary(k => k.Key, v => v.Value);
        }

        private double GetDistanceWithLineString(LineString splitLineString, Node source, Node target)
        {
            var coordinates = splitLineString.Coordinates;
            double distance = 0;
            logger.Debug("Distance 1  = {0}", distance);
            if (!(coordinates[0].X == source.X && coordinates[0].Y == source.Y))
            {
                distance += Helper.GetDistance(coordinates[0].X, coordinates[0].Y, source.X, source.Y);
            }
            logger.Debug("Distance between point {0} {1} and {2} {3} 2 = {4}", coordinates[0].Y, coordinates[0].X, source.Y, source.X, distance);
            int size = coordinates.Count() - 1;
            for (int i = 0; i < size; i++)
            {
                distance += Helper.GetDistance(coordinates[i].X, coordinates[i].Y, coordinates[i + 1].X, coordinates[i + 1].Y);
                logger.Debug("Distance between point {0} {1} and {2} {3} {4}  = {5}", coordinates[i].Y, coordinates[i].X, coordinates[i + 1].Y, coordinates[i + 1].X, 3 + i, distance);
            }
            distance += Helper.GetDistance(coordinates[size].X, coordinates[size].Y, target.X, target.Y);
            logger.Debug("Distance {0} {1} and {2} {3} {4}", coordinates[size].Y, coordinates[size].X, target.Y, target.X, distance);
            return distance;
        }

        public int GetNumberStops()
        {
            return stopDico.Count();
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
                                             where trip.Value.Schedule.Details.First().Value.DepartureTime >= min 
                                                    && trip.Value.Schedule.Details.First().Value.DepartureTime <= max
                                             select trip;
            return tripsForOneDayBetweenHours.ToDictionary(k => k.Key, v => v.Value);
        }

        private LineString CreateLineString(string shapeId)
        {
            var shapeInfos = CtrlCsv.RecordsShape.FindAll(x => x.Id == shapeId);
            // CREATION of LINESTRING
            List<Coordinate> coordinatesList = new List<Coordinate>();
            for (int i = 0; i < shapeInfos.Count; i++)
            {
                ShapeCsv shape = shapeInfos[i];
                Coordinate coordinate = new Coordinate(shape.PtLon, shape.PtLat); // here 
                if (!coordinatesList.Contains(coordinate))
                {
                    coordinatesList.Add(coordinate);
                }
            }
            LineString lineString = new LineString(coordinatesList.ToArray());
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

        private async Task DownloadGtfs()
        {
            string path = System.IO.Path.GetFullPath("GtfsData");

            logger.Info("Start download {0}", choice);
            string fullPathDwln = $"{path}{Path.DirectorySeparatorChar}{choice}{Path.DirectorySeparatorChar}gtfs.zip";
            string fullPathExtract = $"{path}{Path.DirectorySeparatorChar}{choice}{Path.DirectorySeparatorChar}gtfs";
            Uri linkOfGtfs = Configuration.ProvidersInfo[choice];
            Directory.CreateDirectory(path);
            Directory.CreateDirectory($"{path}{Path.DirectorySeparatorChar}{choice}");

            Task dwnldAsync;

            using (WebClient wc = new WebClient())
            {
                dwnldAsync = wc.DownloadFileTaskAsync(
                    // Param1 = Link of file
                    linkOfGtfs,
                    // Param2 = Path to save
                    fullPathDwln);
                logger.Info("downloaded directory for {0}", choice);
            }
            try
            {
                await dwnldAsync;
            }
            catch
            {
                logger.Info("Error with the provider {0}", choice);
                throw;
            }
            await Task.Run(() => ZipFile.ExtractToDirectory(fullPathDwln, fullPathExtract));
            logger.Info("{0} done", choice);

            if (Directory.Exists(fullPathExtract))
            {
                File.Delete(fullPathDwln); //delete .zip
            }
        }
    }
}