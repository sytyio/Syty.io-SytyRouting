using NLog;
using SytyRouting.Gtfs.ModelCsv;
using System.Diagnostics;
using System.Globalization;
using SytyRouting.Model;
using SytyRouting.Gtfs.ModelGtfs;
using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using System.IO.Compression; //zip
using NetTopologySuite.Operation.Distance;
using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Geometries.Implementation;

namespace SytyRouting.Gtfs.GtfsUtils
{

    public class ControllerGtfs
    {
        static readonly HttpClient client = new HttpClient();

        private static Logger logger = LogManager.GetCurrentClassLogger();

        [NotNull]
        public ControllerCsv? CtrlCsv = null!;

        public bool IsActive=false;


        public int invalidLineStrings = 0;
        public List<string> debugTables = new List<string>();
        public int splitShapeByStopsCount=0;
        public int buildShapeSegmentErrors=0;

        
        private string _provider;

        private const double checkValue = 0.000000000000001;

        [NotNull]
        private Dictionary<string, TripGtfs>? tripDicoForOneDay = null!;

        private static int idGeneratorAgency = int.MaxValue - 10000;

        [NotNull]
        private Dictionary<string, StopGtfs>? stopDico = null!;

        [NotNull]
        private Dictionary<string, RouteGtfs>? routeDico = null!;
        [NotNull]
        private Dictionary<string, ShapeGtfs>? shapeDico = null!;
        [NotNull]
        private Dictionary<string, CalendarGtfs>? calendarDico = null!;
        [NotNull]
        private Dictionary<string, TripGtfs>? tripDico = null!;
        [NotNull]
        private Dictionary<string, AgencyGtfs>? agencyDico = null!;
        [NotNull]
        private Dictionary<string, ScheduleGtfs>? scheduleDico = null!;

        private Dictionary<string, Dictionary<DateTime, int>> calendarDateDico = null!;
        [NotNull]
        private Dictionary<string, Edge>? edgeDico = new Dictionary<string, Edge>();

        // Nearest nodes
        private Dictionary<string, Node> nearestNodeDico = new Dictionary<string, Node>();

        public ControllerGtfs(string provider)
        {
            _provider = provider;
        }

        public async Task Initialize()
        {
            var status = await DownloadGtfs();

            if(status==GTFSDownloadState.Completed)
            {
                CtrlCsv = new ControllerCsv(_provider);
            }
            else
            {
                logger.Info("Error downloading GTFS data for {0}",_provider);
                return;
            }

            //debug:
            var shapeDebuger = new DataBase.DebugGeometryUploader();
            //:gudeb

            var stopWatch = new Stopwatch();

            stopWatch.Start();
            stopDico = CreateStopGtfsDictionary();
            logger.Info("Stop dico nb stops = {0} for {1} in {2}", stopDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));

            stopWatch.Restart();
            agencyDico = CreateAgencyGtfsDictionary();
            logger.Info("Agency nb {0} for {1} in {2}", agencyDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));
            
            stopWatch.Restart();
            routeDico = CreateRouteGtfsDictionary();
            logger.Info("Route nb {0} for {1} in {2}", routeDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));
            
            stopWatch.Restart();
            shapeDico = CreateShapeGtfsDictionary();
            logger.Info("Shape nb {0} for {1} in {2}", shapeDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));

            //debug:
            var connectionString = Configuration.ConnectionString;
            var debugTable = "gtfs_shape_"+_provider;
            await shapeDebuger.SetDebugGeomTable(connectionString,debugTable);
            await shapeDebuger.UploadTrajectoriesAsync(connectionString,debugTable,shapeDico.Values.ToList());

            logger.Debug("Build Shape segment errors: {0}",buildShapeSegmentErrors);
            //:gudeb
            
            stopWatch.Restart();
            calendarDico = CreateCalendarGtfsDictionary();
            calendarDateDico = CreateCalendarDateGtfsDictionary();
            logger.Info("Calendar nb {0} for {1} in {2}", calendarDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));
            SetDaysCirculation();
            
            stopWatch.Restart();
            scheduleDico = CreateScheduleGtfsDictionary();
            logger.Info("Schedule nb {0} for {1} in {2}", scheduleDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));
            
            stopWatch.Restart();
            tripDico = CreateTripGtfsDictionary();
            logger.Info("Trip  nb {0} for {1} in {2}", tripDico.Count, _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));

            stopWatch.Restart();
            AddTripsToRoute();
            logger.Info("Trip to route for {0} in {1}", _provider, Helper.FormatElapsedTime(stopWatch.Elapsed));
            
            
            
            stopWatch.Restart();
            AddSplitLineString();
            
            //debug:
            Task clean = DataBase.RouteUploadBenchmarking.CleanComparisonTablesAsync(connectionString,debugTables);
            Task.WaitAll(clean);
            //:gudeb

            logger.Info("Add split linestring loaded in {0} for {1}", Helper.FormatElapsedTime(stopWatch.Elapsed), _provider);
            


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
            logger.Info("Nb trips for all days = {0} for {1}", nbTrips, _provider);

            AllTripsToEdgeDictionary();

            IsActive=true;
            
            logger.Info("Edge dictionary loaded in {0}", Helper.FormatElapsedTime(stopWatch.Elapsed));

            logger.Debug("Invalid LineStrings loaded for {0}: {1}",_provider,invalidLineStrings);

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
                return CtrlCsv.RecordsRoute.ToDictionary(x => x.Id, x => new RouteGtfs(x.Id, x.LongName, GtfsExtendedTypeToBasicType(x.Type), new Dictionary<string, TripGtfs>(), null));
            }
            return CtrlCsv.RecordsRoute.ToDictionary(x => x.Id, x => new RouteGtfs(x.Id, x.LongName, GtfsExtendedTypeToBasicType(x.Type), new Dictionary<string, TripGtfs>(), GetAgency(x.AgencyId)));
        }

        private int GtfsExtendedTypeToBasicType(int type){
            switch(type){
                case int n when (n<=12):
                    return type;
                case int n when (n==700):
                    return 3;
                case int n when (n>=100 && n<=103):
                    return 2;
                default:
                    throw new ArgumentException(String.Format("Type not implemented for type = {0} see here https://developers.google.com/transit/gtfs/reference/exten ",type));
            }
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
            return CtrlCsv.RecordsShape.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => new ShapeGtfs(x.Key, x.OrderBy(y => y.PtSequence).ToDictionary(y => y.PtSequence, y => (new Point(y.PtLon, y.PtLat))), CreateLineString(x.Key)!));
        }

        private Dictionary<string, ShapeGtfs> CreateShapeGtfsDictionary2()
        {
            return CtrlCsv
                        .RecordsShape
                        .GroupBy(x => x.Id)
                        .ToDictionary(
                            x => x.Key,
                            x => new ShapeGtfs(
                                x.Key,
                                x.OrderBy(y => y.PtSequence).ToDictionary(y => y.PtSequence, y => (new Point(y.PtLon, y.PtLat))), CreateLineString(x.Key)!));
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
                                                                    Convert.ToBoolean(x.Sunday)}, null!, DateTime.ParseExact(x.DateBegin.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture), DateTime.ParseExact(x.DateEnd.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture))); //here
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
                return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, null, scheduleDico[x.Id], GetCalendar(x.ServiceId)!));
            }
            return CtrlCsv.RecordsTrip.ToDictionary(x => x.Id, x => new TripGtfs(routeDico[x.RouteId], x.Id, GetShape(x.ShapeId), scheduleDico[x.Id], GetCalendar(x.ServiceId)!));
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
            if (shapeId != null && shapeDico.ContainsKey(shapeId))
            {
                return shapeDico[shapeId];
            }
            return null;
        }

        private void AllTripsToEdgeDictionary()
        {
            int oneTripToEdgeDictionaryErrors = 0;

            if (Configuration.SelectedDate == "")
            {
                foreach (var trip in tripDico)
                {
                    oneTripToEdgeDictionaryErrors+=OneTripToEdgeDictionary(trip.Key);
                }
            }
            else
            {
                foreach (var trip in tripDicoForOneDay) // trips for one day
                {
                    oneTripToEdgeDictionaryErrors+=OneTripToEdgeDictionary(trip.Key);
                }
            }

            logger.Debug("{0} OneTripToEdgeDictionary errors for provider {1}",oneTripToEdgeDictionaryErrors,_provider);
        }

        private void AddSplitLineString()
        {
            foreach (var trip in tripDico)
            {

                //debug:
                if (trip.Key.Equals("35535597-B_2023-BW_A_EX-Sem-N-3-10"))
                {
                    //;
                    Console.WriteLine("trip: 35535597-B_2023-BW_A_EX-Sem-N-3-10");
                }
                //:gudeb


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
                    currentShape.SplitLineString = SplitLineStringByPoints(currentShape.LineString, stopsCoordinatesArray, currentShape.Id, trip.Key);
                    var dumm = SplitShapeByStops(currentShape.LineString, stopsCoordinatesArray, currentShape.Id, trip.Key);
                }
            }
        }

        public List<LineString> SplitLineStringByPoints(LineString ls, Point[] pts, string shapeId, string tripKey)
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

            //debug:
            //string shapeIdRef = "012b0234"; // STIB
            string shapeIdRef = "B00100017"; // TEC
            if (shapeId.Equals(shapeIdRef))
            {
                //;

                Console.WriteLine("shapeId = {0}, tripKey = {1}",shapeIdRef,tripKey);

                //debug:
                
                var shapeDebuger = new DataBase.DebugGeometryUploader();

                List<KeyValuePair<string,LineString>> trajectoriesIdLineStringPairs = new List<KeyValuePair<string,LineString>>();
                
                var connectionString = Configuration.ConnectionString;
                var debugTable = "gtfs_shape_"+shapeId+"_trip_"+tripKey.Replace("-", "_");;
                debugTables.Add(debugTable);
                Task setTable = shapeDebuger.SetDebugGeomTable(connectionString,debugTable);

                trajectoriesIdLineStringPairs.Add(new KeyValuePair<string,LineString>(tripKey,ls));
                foreach (var part in parts)
                {
                    trajectoriesIdLineStringPairs.Add(new KeyValuePair<string,LineString>(shapeId,part));
                }
                
                Task.WaitAll(setTable);

                Task uploadTrajectories = shapeDebuger.UploadTrajectoriesAsync(connectionString,debugTable,trajectoriesIdLineStringPairs);
                Task.WaitAll(uploadTrajectories);

                //:gudeb
            }
            //:gudeb

            return parts;
        }

        public List<LineString> SplitShapeByStops(LineString lineString, Point[] stops, string shapeId, string tripKey)
        {
            // This method assumes that (1) the Stop coordinates (X,Y) are properly ordered by the M-ordinate (stop_sequence on the GTFS stop_times.txt file),
            // (2) the Stop coordinates are "sufficiently" close to the LineString that represents the GTFS shape (or perhaps equivalent, the LineString is sufficiently smooth).

            List<LineString> segments = new List<LineString>();
            
            var coordinates = lineString.Coordinates.OrderBy(c=>c.M).ToList();
            PriorityQueue<Coordinate,Double> sequentiallyExplodedLineString = sequenciallyExplodeLineString(lineString);

            int startIndex = 0;
            int endIndex = 0;

            startIndex = GetFirstInLineNearestPointIndex(startIndex,stops[0],coordinates);
            if(startIndex < 0)
            {
                Console.WriteLine("Wait a minute!");
                startIndex = 0; // The slam-it! solution.
            }

            for (int i = 1; i < stops.Length; ++i)
            {
                endIndex = GetFirstInLineNearestPointIndex(startIndex,stops[i],coordinates);

                segments.Add(BuildShapeSegment(startIndex,endIndex,coordinates));

                if (endIndex > startIndex) // Otherwise skip faulty endIndices (that probably led to empty Shape segments in the previous calculation)
                {
                    startIndex = endIndex;
                }
            }

            //debug:
            //string shapeIdRef = "012b0234"; // STIB
            string shapeIdRef = "B00100017"; // TEC
            if (shapeId.Equals(shapeIdRef))
            {
                //;

                Console.WriteLine("shapeId = {0}, tripKey = {1}",shapeIdRef,tripKey);

                //debug:
                
                var shapeDebuger = new DataBase.DebugGeometryUploader();

                List<KeyValuePair<string,LineString>> trajectoriesIdLineStringPairs = new List<KeyValuePair<string,LineString>>();
                
                var connectionString = Configuration.ConnectionString;
                var debugTable = "gtfs_shape_"+shapeId+"_trip_"+tripKey.Replace("-", "_").Remove(10)+"_s"+splitShapeByStopsCount;
                debugTables.Add(debugTable);
                Task setTable = shapeDebuger.SetDebugGeomTable(connectionString,debugTable);

                trajectoriesIdLineStringPairs.Add(new KeyValuePair<string,LineString>(tripKey,lineString));
                foreach (var segment in segments)
                {
                    trajectoriesIdLineStringPairs.Add(new KeyValuePair<string,LineString>(shapeId,segment));
                }
                
                Task.WaitAll(setTable);

                Task uploadTrajectories = shapeDebuger.UploadTrajectoriesAsync(connectionString,debugTable,trajectoriesIdLineStringPairs);
                Task.WaitAll(uploadTrajectories);

                ++splitShapeByStopsCount;

                //:gudeb
            }
            //:gudeb

            return segments;
        }

        private LineString BuildShapeSegment(int startIndex, int endIndex, List<Coordinate> coordinates)
        {
            DotSpatialAffineCoordinateSequenceFactory _sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            GeometryFactory _geometryFactory = new GeometryFactory(_sequenceFactory);

            if (startIndex <  0 || endIndex < 0  || endIndex <= startIndex)
            {
                logger.Debug("Unable to process Shape segment. Indices: {0} -> {1}", startIndex, endIndex);
                var _emptyLineString = new LineString(null, _geometryFactory);
                ++buildShapeSegmentErrors;

                return _emptyLineString;
            }

            List<Coordinate> xyCoordinates = new List<Coordinate>(coordinates.Count); // number of nodes +1 start point (home) +1 end point (work)

            for (int i = startIndex; i <= endIndex; ++i)
            {
                xyCoordinates.Add(new Coordinate(coordinates[i].X,coordinates[i].Y));
            }

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(xyCoordinates, Ordinates.XYM);

            return new LineString(coordinateSequence, _geometryFactory);
        }

        private int GetFirstInLineNearestPointIndex(int previousIndex, Point stop, List<Coordinate> sequence)
        {
            int index = -1;
            double tolerance = 10.0; // [m]
            Dictionary<int,double> candidates = new Dictionary<int, double>();

            //double minDistance = double.PositiveInfinity;
            double distance = 0.0;
            double previousDistance = double.PositiveInfinity;

            for (int i = previousIndex; i < sequence.Count; ++i)
            {
                distance = Helper.GetDistance(stop.X,stop.Y,sequence[i].X,sequence[i].Y);
                if (distance < tolerance && !candidates.ContainsKey(i))
                {
                    candidates.Add(i,distance);
                }
            }


            var indices = candidates.Keys.OrderBy(i=>i);
            foreach (int i in indices)
            {
                if (candidates[i] > previousDistance)
                {
                    break;
                }
                else
                {
                    previousDistance = candidates[i];
                    index = i;
                }
            }

            return index;
        }


        private PriorityQueue<Coordinate,double> sequenciallyExplodeLineString(LineString lineString)
        {
            PriorityQueue<Coordinate,double> explodedLineString = new PriorityQueue<Coordinate,double>(lineString.Coordinates.Count());

            try
            {
                var coordinates = lineString.Coordinates.ToDictionary(c=>c.M,c=>c);

                foreach (var coordinate in coordinates)
                {
                    explodedLineString.Enqueue(coordinate.Value,coordinate.Key);
                }
            }
            catch (Exception e)
            {
                logger.Debug("Unable to convert Coordinates Array to an M-index Dictionary: {0}",e.Message);
            }
            
            return  explodedLineString;
        }

        private int OneTripToEdgeDictionary(string tripId)
        {
            int oneTripToEdgeDictionaryErrors = 0;

            //debug:
            if (tripId.Equals("35535597-B_2023-BW_A_EX-Sem-N-3-10"))
            {
                ;
            }
            //:gudeb

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

            int index = 1;
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

                        EdgeGtfs newEdge = null!;

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

                            try
                            {
                                LineString splitLineString = buffShape.SplitLineString[index];
                                var internalGeom = Helper.GetInternalGeometry(splitLineString, OneWayState.Yes);

                                //debug:
                                //Console.WriteLine("index: {0} buffShape.SplitLineString.Count: {1}",index,buffShape.SplitLineString.Count);
                                //:gudeb
                            
                                if(internalGeom != null)
                                   newEdge = AddEdge(splitLineString, currentNearestNodeOnLineString, previousNearestOnLineString, newId, duration, buffTrip, internalGeom, previousStop, currentStop);
                            }
                            catch (Exception e)
                            {
                                logger.Debug("SplitLineString error: {0}",e.Message);
                                //logger.Debug("index: {0} buffShape.SplitLineString.Count: {1}",index,buffShape.SplitLineString.Count);
                                oneTripToEdgeDictionaryErrors++;
                            }

                            if(newEdge !=null && (newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0))
                            {
                                //TraceGTFSTrip(buffTrip,previousStop,currentStop,newEdge);
                            }

                            index++;
                        }
                        else // if there is no linestring
                        {
                                //debug:
                                Console.WriteLine("No LineString");
                                //:gudeb

                            currentNearestNodeOnLineString.X = currentStop.X;
                            currentNearestNodeOnLineString.Y = currentStop.Y;

                            var idForNearestNode = newId + "N";
                            if (!nearestNodeDico.ContainsKey(idForNearestNode))
                            {
                                AddNearestNodeCreateEdges(currentStop, currentNearestNodeOnLineString, idForNearestNode, buffTrip, 0, cpt); // if there is no lineString nearest and stop are at the same coordinates
                            }
                            newEdge = new EdgeGtfs(newId, buffShape!.Id, buffTrip.Route.Id, tripId, previousStop, currentStop, distance, duration, buffTrip.Route, false, distance / duration, null, TransportModes.PublicModes, buffTrip.Route.Type);
                            AddEdgeToNodes(previousNearestOnLineString, currentNearestNodeOnLineString, newEdge, buffTrip, previousStop, currentStop);
                            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                                //logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,previousStop.Id,previousStop.Y,previousStop.X,currentStop.Id,currentStop.Y,currentStop.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
                                //TraceGTFSTrip(buffTrip,previousStop,currentStop,newEdge);
                            }
                        }
                    }
                    else
                    {                       
                        index++;

                        //debug:
                        //Console.WriteLine("edgeDico Contains Key newId. index: {0}",index);
                        //:gudeb
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

                //debug:
                if(_provider.Equals("Le_TEC") && index == buffShape?.SplitLineString.Count-1)
                {
                    ;
                }
                //:gudeb

                previousStopTime = currentStopTime.Value;
                previousNearestOnLineString = currentNearestNodeOnLineString;
            }

            return oneTripToEdgeDictionaryErrors;
        }

        private EdgeGtfs AddEdge(LineString splitLineString, Node currentNearestNodeOnLineString, Node previousNearestOnLineString, string newId, double duration, TripGtfs buffTrip, XYMPoint[] internalGeom, StopGtfs prev, StopGtfs current) // StopGtfs prev, StopGtfs current
        {
            var distance = GetDistanceWithLineString(splitLineString, currentNearestNodeOnLineString, previousNearestOnLineString, buffTrip);
            var newEdge = new EdgeGtfs(newId,  buffTrip.Shape!.Id, buffTrip.Route.Id, buffTrip.Id, previousNearestOnLineString, currentNearestNodeOnLineString, distance, duration, buffTrip.Route, true,
                                      distance / duration, internalGeom, TransportModes.PublicModes, buffTrip.Route.Type);
            AddEdgeToNodes(previousNearestOnLineString, currentNearestNodeOnLineString, newEdge, buffTrip, prev, current);
            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                //logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,prev.Id,prev.Y,prev.X,current.Id,current.Y,current.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
                //TraceGTFSTrip(buffTrip,previousStop,currentStop,newEdge);
            }
            return newEdge;
        }

        private void AddEdgeToNodes(Node previousStop, Node currentStop, EdgeGtfs newEdge, TripGtfs buffTrip, StopGtfs prev, StopGtfs current) // bufftrip prev current
        {
            previousStop.OutwardEdges.Add(newEdge);
            currentStop.InwardEdges.Add(newEdge);
            edgeDico.Add(newEdge.Id, newEdge);
            if(newEdge.LengthM<=0||newEdge.MaxSpeedMPerS<=0||Double.IsNaN(newEdge.LengthM)||newEdge.MaxSpeedMPerS>50||newEdge.DurationS<=0){
                //logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",buffTrip.Route.Id,buffTrip.Id,prev.Id,prev.Y,prev.X,current.Id,current.Y,current.X,newEdge.LengthM,newEdge.MaxSpeedMPerS,newEdge.DurationS);
                //TraceGTFSTrip(buffTrip,previousStop,currentStop,newEdge);
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
            var edgeWalkStopToNearest = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.DefaultMode, SourceNode = currentStop, TargetNode = currentNearestNodeOnLineString, MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.DefaultMode], TagIdRouteType=TransportModes.GtfsDefaultFoot};            
            var edgeWalkNearestToStop = new Edge { OsmID = long.MaxValue, LengthM = distance, TransportModes = TransportModes.DefaultMode, SourceNode = currentNearestNodeOnLineString, TargetNode = currentStop, MaxSpeedMPerS = TransportModes.MasksToSpeeds[TransportModes.DefaultMode], TagIdRouteType=TransportModes.GtfsDefaultFoot };
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
            //     logger.Debug("Delta distance with linestring and without = {0} for {1}", Math.Abs(distance - Helper.GetDistance(source, target)),_provider);
            // }
            return distance;
        }

        public Dictionary<string, TripGtfs> SelectAllTripsForMondayBetween10and11(Dictionary<string, TripGtfs> tripDico, Dictionary<string, ScheduleGtfs> scheduleDico)
        {
            return SelectAllTripsForGivenDayAndBetweenGivenHours(new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0), 6);
        }

        /**
         0 for monday, 1 for tuesday, 2 for wednesday, 3 for thursday, 4 for friday, 5 for saturday, 6 for sunday
        **/

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
            //debug:
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var geometryFactory = new GeometryFactory(sequenceFactory);
            //:gudeb
            
            //debug: 
            //var shapeInfos = CtrlCsv.RecordsShape.FindAll(x => x.Id == shapeId);
            var shapeInfos = CtrlCsv.RecordsShape.FindAll(x => x.Id == shapeId).OrderBy(x => x.PtSequence).ToList();
            //:gudeb

            // CREATION of LINESTRING
            List<Coordinate> coordinatesList = new List<Coordinate>();
            
            //debug:
            List<double> mOrdinates = new List<double>();
            //:gudeb

            for (int i = 0; i < shapeInfos.Count; i++)
            {
                ShapeCsv shape = shapeInfos[i];
                Coordinate coordinate = new Coordinate(shape.PtLon, shape.PtLat); // here 
                if (!coordinatesList.Contains(coordinate))
                {
                    coordinatesList.Add(coordinate);
                    mOrdinates.Add((double)shape.PtSequence);
                }
            }
            if (coordinatesList.Count() < 2)
            {
                return null;
            }

            var coordinateSequence = new DotSpatialAffineCoordinateSequence(coordinatesList.ToArray(), Ordinates.XYM);
            for(var i = 0; i < coordinateSequence.Count; i++)
            {
                coordinateSequence.SetM(i, mOrdinates[i]);
            }
            coordinateSequence.ReleaseCoordinateArray();
            
            //debug: LineString lineString = new LineString(coordinatesList.ToArray());
            
            //return new LineString(coordinateSequence, geometryFactory);
            var lineString = new LineString(coordinateSequence, geometryFactory);


            if(!TestBench.isValidSequence(mOrdinates.ToArray()))
            {
                //logger.Debug("Invalid mOrdinate sequence: Shape {0}", shapeId);
                //TestBench.TraceLineStringRoute(lineString);
                ++invalidLineStrings;
            } 

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

        private async Task<GTFSDownloadState> DownloadGtfs()
        {
            logger.Info("Fetching GTFS data for {0}", _provider);

            GTFSDownloadState state = GTFSDownloadState.Error;
            
            string path = System.IO.Path.GetFullPath("GtfsData");         
            Uri linkOfGtfs = Configuration.ProvidersInfo[_provider].Uri;
            string zipFile = Configuration.ProvidersInfo[_provider].ZipFile;

            string fullPathDwln = $"{path}{Path.DirectorySeparatorChar}{_provider}{Path.DirectorySeparatorChar}{zipFile}";
            string fullPathExtract = $"{path}{Path.DirectorySeparatorChar}{_provider}{Path.DirectorySeparatorChar}gtfs";
            
            Directory.CreateDirectory(path);
            Directory.CreateDirectory($"{path}{Path.DirectorySeparatorChar}{_provider}");
            
            try
            {
                using HttpResponseMessage response = await client.GetAsync(linkOfGtfs, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(fullPathDwln, FileMode.CreateNew))
                {
                    await response.Content.CopyToAsync(fs);
                }

                logger.Info("Extrancting GTFS files to {0}",fullPathExtract);
                ZipFile.ExtractToDirectory(fullPathDwln, fullPathExtract);
                
                logger.Info("Downloading GTFS files for {0} completed", _provider);

                if (Directory.Exists(fullPathExtract))
                {
                    File.Delete(fullPathDwln); //delete .zip
                }
                logger.Info("GTFS source file for {0} deleted", _provider);

                return GTFSDownloadState.Completed;
            }
            catch(Exception e)
            {
                logger.Info("Unable to download GTFS data for {0}: {1}",_provider,e.Message);
                
                return state;
            }
        }

        private void TraceGTFSTrip(TripGtfs trip, StopGtfs previousStop, StopGtfs currentStop, EdgeGtfs edge)
        {
            logger.Info("Route {0} Trip {1} from {2} {3} {4} to {5} {6} {7} length {8} speed {9} duration {10}",trip.Route.Id,trip.Id,previousStop.Id,previousStop.Y,previousStop.X,currentStop.Id,currentStop.Y,currentStop.X,edge.LengthM,edge.MaxSpeedMPerS,edge.DurationS);
        }
    }
}