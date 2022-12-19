using NLog;
using SytyRouting.Gtfs.ModelCsv;
using System.Diagnostics;
using System.Globalization;
using SytyRouting.Model;
using SytyRouting.Gtfs.ModelGtfs;
using NetTopologySuite.Geometries;
using System.Net; //download file 
using System.IO.Compression; //zip
using NetTopologySuite.Operation.Distance;
using System.Diagnostics.CodeAnalysis;
namespace SytyRouting.Gtfs.GtfsUtils
{

    public class ControllerGtfs
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [NotNull]
        public ControllerCsv? CtrlCsv;
        private string choice;

        private const double checkValue = 0.000000000000001;

        [NotNull]
        private Dictionary<string, TripGtfs>? tripDicoForOneDay;

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

        private Dictionary<string, Dictionary<DateTime, int>> calendarDateDico;
        [NotNull]
        private Dictionary<string, Edge>? edgeDico = new Dictionary<string, Edge>();

        // Nearest nodes
        private Dictionary<string, Node> nearestNodeDico = new Dictionary<string, Node>();

        public ControllerGtfs(string provider)
        {
            choice = provider;
        }

        public async Task InitController()
        {
            await DownloadGtfs();
            CtrlCsv = new ControllerCsv(choice);

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

            calendarDateDico = CreateCalendarDateGtfsDictionary();
            logger.Info("Calendar nb {0} for {1} in {2}", calendarDico.Count, choice, Helper.FormatElapsedTime(stopWatch.Elapsed));
            SetDaysCirculation();
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
            logger.Info("Add split linestring loaded in {0} for {1}", Helper.FormatElapsedTime(stopWatch.Elapsed), choice);
            stopWatch.Restart();


            var nbTrips = tripDico.Count();
            if (Configuration.SelectedDate != "")
            {
                var givenDateParse = DateTime.ParseExact(Configuration.SelectedDate, "yyyyMMdd", CultureInfo.InvariantCulture);
                var tripsGivenDay = from trip in tripDico
                                    where trip.Value.CalendarInfos.Dates.Contains(givenDateParse)
                                    select trip;
                tripDicoForOneDay = tripsGivenDay.ToDictionary(x => x.Key, x => x.Value);
                var nbTripsDays = tripDicoForOneDay.Count();
                logger.Info("Nb trips for one day ( {0} ) = {1}", Configuration.SelectedDate, nbTripsDays);
            }

            logger.Info("Nb trips for all days = {0} for {1}", nbTrips, choice);


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
                                                                    Convert.ToBoolean(x.Sunday)}, null, DateTime.ParseExact(x.DateBegin.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture), DateTime.ParseExact(x.DateEnd.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture))); //here
        }

        private Dictionary<string, Dictionary<DateTime, int>> CreateCalendarDateGtfsDictionary()
        {
            return CtrlCsv.RecordsCalendarDates.GroupBy(x => x.ServiceId).ToDictionary(x => x.Key, x => x.ToDictionary(y => DateTime.ParseExact(y.DateException, "yyyyMMdd", CultureInfo.InvariantCulture), y => y.ExceptionType));
        }

        private void SetDaysCirculation()
        {
            foreach (var calendar in calendarDico)
            {
                var buffServiceId = calendar.Value.ServiceId;
                List<DateTime> datesOfCirculation = new List<DateTime>();
                var arrayDays = calendar.Value.Days;
                if (arrayDays[0])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Monday));
                }
                if (arrayDays[1])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Tuesday));
                }
                if (arrayDays[2])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Wednesday));
                }
                if (arrayDays[3])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Thursday));
                }
                if (arrayDays[4])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Friday));
                }
                if (arrayDays[5])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Saturday));
                }
                if (arrayDays[6])
                {
                    datesOfCirculation.AddRange(Helper.GetWeekdayInRange(calendar.Value.DateBegin, calendar.Value.DateEnd, DayOfWeek.Sunday));
                }

                foreach (var item in calendarDateDico)
                {
                    if (item.Key == buffServiceId)
                    {
                        foreach (var dateInfo in item.Value)
                        {
                            if (dateInfo.Value == 1)
                            {
                                // If exceptionType = 1 : the trip is added
                                datesOfCirculation.Add(dateInfo.Key);
                            }
                            else
                            {
                                // if exceptionType = 2 : the trip is deleted
                                datesOfCirculation.Remove(dateInfo.Key);
                            }
                        }
                    }
                }
                calendar.Value.Dates = datesOfCirculation;
            }
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
                return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, null, scheduleDico[x.Id], GetCalendar(x.ServiceId)));
            }
            return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, GetShape(x.ShapeId), scheduleDico[x.Id], GetCalendar(x.ServiceId)));
        }

        private CalendarGtfs? GetCalendar(string serviceId)
        {
            if (calendarDico.ContainsKey(serviceId))
            {
                return calendarDico[serviceId];
            }
            return null;
        }

        private ShapeGtfs? GetShape(string? shapeId)
        {
            if (shapeDico.ContainsKey(shapeId))
            {
                return shapeDico[shapeId];
            }
            return null;
        }

        private void AllTripsToEdgeDictionary()
        {
            if (Configuration.SelectedDate == "")
            {

                foreach (var trip in tripDico)
                {
                    OneTripToEdgeDictionary(trip.Key);
                }
            }
            else
            {
                foreach (var trip in tripDicoForOneDay) // trips for one day
                {
                    OneTripToEdgeDictionary(trip.Key);
                }
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
                    currentShape.SplitLineString = Helper.SplitLineStringByPoints(currentShape.LineString, stopsCoordinatesArray, currentShape.Id);
                }
            }
        }

        private void OneTripToEdgeDictionary(string tripId)
        {
            TripGtfs buffTrip;
            if (Configuration.SelectedDate == "")
            {
                buffTrip = tripDico[tripId]; // All trips
            }
            else
            {
                buffTrip = tripDicoForOneDay[tripId]; // Trips for one day 
            }
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
                if (previousStop != null && previousStopTime != null && previousNearestOnLineString != null)
                {
                    currentStop.ValidSource = true;
                    string newId = "FROM" + previousStop.Id + "TO" + currentStop.Id + "IN" + buffTrip.Route.Id;
                    if (!edgeDico.ContainsKey(newId))
                    {
                        double distance = Helper.GetDistance(previousStop.X, previousStop.Y, currentStop.X, currentStop.Y); // Replace by distance with de splitLineString 
                        TimeSpan arrival = currentStopTime.Value.ArrivalTime;
                        TimeSpan departure = previousStopTime.DepartureTime;

                        // temporary solution to include waiting time (later: use of a penalty)
                        var watchTime = (previousStopTime.DepartureTime - previousStopTime.ArrivalTime).TotalSeconds;
                        var duration = (arrival - departure).TotalSeconds + watchTime;
                        if (duration < 0)
                        {
                            // logger.Info("duration neg  {0}",duration);
                            duration += 86400; // for specific cases like arrival current at 00:02:00 but departure previous at 23:57:00
                        }
                        EdgeGtfs newEdge;

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
                                AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, length, cpt);
                            }
                            LineString lineString = buffShape.LineString;

                            LineString splitLineString = buffShape.SplitLineString[i];
                            var internalGeom = Helper.GetInternalGeometry(splitLineString, OneWayState.Yes);
                            newEdge = AddEdge(splitLineString, currentNearestNodeOnLineString, previousNearestOnLineString, newId, duration, buffTrip, internalGeom, previousStop, currentStop);
                                    if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                                logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,previousStop.Id,previousStop.Y,previousStop.X,currentStop.Id,currentStop.Y,currentStop.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
            }
                            i++;
                        }
                        else // if there is no linestring
                        {
                            currentNearestNodeOnLineString.X = currentStop.X;
                            currentNearestNodeOnLineString.Y = currentStop.Y;

                            var idForNearestNode = newId + "N";
                            if (!nearestNodeDico.ContainsKey(idForNearestNode))
                            {
                                AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, 0, cpt); // if there is no lineString nearest and stop are at the same coordinates
                            }
                            newEdge = new EdgeGtfs(newId, previousStop, currentStop, distance, duration, buffTrip.Route, false, distance / duration, null, TransportModes.PublicModes, buffTrip.Route.Type);
                            AddEdgeToNodes(previousNearestOnLineString, currentNearestNodeOnLineString, newEdge, buffTrip, previousStop, currentStop);
                            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                                logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,previousStop.Id,previousStop.Y,previousStop.X,currentStop.Id,currentStop.Y,currentStop.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
            }
                        }
                    }
                    else
                    {
                        i++;
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
                            AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, length, cpt);
                        }
                    }
                    else
                    {
                        var idForNearestNode = currentStop.Id + "IN" + buffTrip.Route.Id + "N";
                        if (!nearestNodeDico.ContainsKey(idForNearestNode))
                        {
                            currentNearestNodeOnLineString.X = currentStop.X;
                            currentNearestNodeOnLineString.Y = currentStop.Y;
                            AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, 0, cpt);
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

        private EdgeGtfs AddEdge(LineString splitLineString, Node currentNearestNodeOnLineString, Node previousNearestOnLineString, string newId, double duration, TripGtfs buffTrip, XYMPoint[] internalGeom, StopGtfs prev, StopGtfs current) // StopGtfs prev, StopGtfs current
        {
            var distance = GetDistanceWithLineString(splitLineString, currentNearestNodeOnLineString, previousNearestOnLineString, buffTrip);
            var newEdge = new EdgeGtfs(newId, previousNearestOnLineString, currentNearestNodeOnLineString, distance, duration, buffTrip.Route, true,
                                      distance / duration, internalGeom, TransportModes.PublicModes, buffTrip.Route.Type);
            AddEdgeToNodes(previousNearestOnLineString, currentNearestNodeOnLineString, newEdge, buffTrip, prev, current);
            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,prev.Id,prev.Y,prev.X,current.Id,current.Y,current.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
            }
            return newEdge;
        }



        private void AddEdgeToNodes(Node previousStop, Node currentStop, EdgeGtfs newEdge, TripGtfs buffTrip, StopGtfs prev, StopGtfs current) // bufftrip prev current
        {
            previousStop.OutwardEdges.Add(newEdge);
            currentStop.InwardEdges.Add(newEdge);
            edgeDico.Add(newEdge.Id, newEdge);
            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,prev.Id,prev.Y,prev.X,current.Id,current.Y,current.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
            }
        }


        private void AddNearestNodeCreateEdges(StopGtfs currentStop, Node currentNearestNodeOnLineString, string id, TripGtfs buffTrip, double distance, int cpt)
        {
            nearestNodeDico.Add(id, currentNearestNodeOnLineString);
            if (Double.IsNaN(distance) || distance == 0)
            {
                distance = 1;
            }
            // The edges from stop to nearest node and back
            //Temporary : use of Bicyclette type for the walk between de stop and the nearest point on the linestring
            var edgeWalkStopToNearest = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.GetTransportModeMask("Foot"), SourceNode = currentStop, TargetNode = currentNearestNodeOnLineString, MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.GetTransportModeMask("Foot")] };
            var edgeWalkNearestToStop = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.GetTransportModeMask("Foot"), SourceNode = currentNearestNodeOnLineString, TargetNode = currentStop, MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.GetTransportModeMask("Foot")] };
            // Add the edges to the nodes
            if (cpt != 0)
            {
                currentNearestNodeOnLineString.OutwardEdges.Add(edgeWalkNearestToStop);
                currentStop.InwardEdges.Add(edgeWalkNearestToStop);
            }

            if (cpt != buffTrip.Schedule.Details.Count - 1)
            {
                currentStop.OutwardEdges.Add(edgeWalkStopToNearest);
                currentNearestNodeOnLineString.InwardEdges.Add(edgeWalkStopToNearest);
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
                        where trip.Value.CalendarInfos.Days[day] == true
                        select trip;
            return query.ToDictionary(k => k.Key, v => v.Value);
        }

        private double GetDistanceWithLineString(LineString splitLineString, Node source, Node target, TripGtfs buffTrip)
        {
            var coordinates = splitLineString.Coordinates;
            double distance = 0;
            int size = coordinates.Count() - 1;
            for (int i = 0; i < size; i++)
            {
                distance += Helper.GetDistance(coordinates[i].X, coordinates[i].Y, coordinates[i + 1].X, coordinates[i + 1].Y);
            }
            // if (Math.Abs(distance - Helper.GetDistance(source, target)) > 2000)
            // {
            //     logger.Debug("Delta distance with linestring and without = {0} for {1}", Math.Abs(distance - Helper.GetDistance(source, target)),choice);
            // }
            return distance;
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

        private LineString? CreateLineString(string shapeId)
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
            if (coordinatesList.Count() < 2)
            {
                return null;
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