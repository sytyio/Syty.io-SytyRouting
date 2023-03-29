using NLog;
using CsvHelper;
using System.Globalization;
using SytyRouting.Gtfs.ModelCsv;

namespace SytyRouting.Gtfs.GtfsUtils
{
    public class ControllerCsv
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public List<StopCsv> RecordsStop { get; }
        public List<RouteCsv> RecordsRoute { get; }
        public List<TripCsv> RecordsTrip { get; }
        public List<ShapeCsv> RecordsShape { get; }
        public List<StopTimesCsv> RecordStopTime { get; }
        public List<CalendarCsv> RecordsCalendar { get; }
        public List<AgencyCsv> RecordsAgency { get; }

        public List<CalendarDateCsv> RecordsCalendarDates {get;}

        public ControllerCsv(string choice)
        {
            RecordsStop = GetAllStops(choice);
            RecordsRoute = GetAllRoutes(choice);
            RecordsTrip = GetAllTrips(choice);
            RecordsShape = GetAllShapes(choice);
            RecordStopTime = GetAllStopTimes(choice);
            RecordsCalendar = GetAllCalendars(choice);
            RecordsAgency = GetAllAgencies(choice);
            RecordsCalendarDates = GetAllCalendarDates(choice);
        }

        private List<StopTimesCsv> GetAllStopTimes(string provider)
        {
            // stop times of chosen society 
            string fullPathTimes = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}stop_times.txt");
            try
            {
                using (var reader = new StreamReader(fullPathTimes))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<StopTimesCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }

        private List<ShapeCsv> GetAllShapes(string provider)
        {
            try
            {
                // Shapes of chosen society
                string fullPathShape = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}shapes.txt");
                using (var reader = new StreamReader(fullPathShape))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<ShapeCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
            catch (FileNotFoundException)
            {
                logger.Info("No given shapes for {0} (file empty or not present)", provider);
                return new List<ShapeCsv>();
            }
        }

        private List<AgencyCsv> GetAllAgencies(string provider)
        {
            string fullPathStop = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}agency.txt");
            try
            {
                using (var reader = new StreamReader(fullPathStop))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<AgencyCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }

        private List<StopCsv> GetAllStops(string provider){
            string fullPathStop = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}stops.txt");
                try
                {
                    using (var reader = new StreamReader(fullPathStop))
                    {
                        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                        {
                            return csv.GetRecords<StopCsv>().ToList();
                        }
                    }
                }
                //catch (DirectoryNotFoundException)
                catch (Exception e)
                {
                    logger.Info("Something went wrong with the {0} directory (missing gtfs): {1}",provider,e.Message);
                    throw;
                }
        }

        private List<CalendarCsv> GetAllCalendars(string provider)
        {
            // Calendar of chosen society
            string fullPathCalendar = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}calendar.txt");
            try
            {
                using (var reader = new StreamReader(fullPathCalendar))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<CalendarCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
            catch (FileNotFoundException)
            {
                logger.Info("No given calendar for {0} (file empty or not present)", provider);
                return new List<CalendarCsv>();
            }
        }

        private List<RouteCsv> GetAllRoutes(string provider)
        {
            // routes of chosen society
            string fullPathRoute = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar}routes.txt");
            try
            {
                using (var reader = new StreamReader(fullPathRoute))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<RouteCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }

        private List<TripCsv> GetAllTrips(string provider)
        {
            // Trips of chosen society
            string fullPathTrip = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar }trips.txt");
            try
            {
                using (var reader = new StreamReader(fullPathTrip))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<TripCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }

        private List<CalendarDateCsv> GetAllCalendarDates(string provider)
        {
            // Trips of chosen society
            string fullPathTrip = System.IO.Path.GetFullPath($"GtfsData{Path.DirectorySeparatorChar}{provider}{Path.DirectorySeparatorChar}gtfs{Path.DirectorySeparatorChar }calendar_dates.txt");
            try
            {
                using (var reader = new StreamReader(fullPathTrip))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        return csv.GetRecords<CalendarDateCsv>().ToList();
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }
    }
}