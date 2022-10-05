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

        public ControllerCsv(ProviderCsv choice)
        {
            RecordsStop = GetAllStops(choice);
            RecordsRoute = GetAllRoutes(choice);
            RecordsTrip = GetAllTrips(choice);
            RecordsShape = GetAllShapes(choice);
            RecordStopTime = GetAllStopTimes(choice);
            RecordsCalendar = GetAllCalendars(choice);
            RecordsAgency = GetAllAgencies(choice);
        }

        public List<StopTimesCsv> GetAllStopTimes(ProviderCsv provider)
        {
            // stop times of chosen society 
            string fullPathTimes = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\stop_times.txt");
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

        public List<ShapeCsv> GetAllShapes(ProviderCsv provider)
        {
            try
            {
                // Shapes of chosen society
                string fullPathShape = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\shapes.txt");
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
                logger.Info("No given shapes (file empty or not present)");
                return new List<ShapeCsv>();
            }
        }

        public List<AgencyCsv> GetAllAgencies(ProviderCsv provider)
        {
            string pathStop = $"GtfsData\\{provider}\\gtfs\\agency.txt";
            string fullPathStop = System.IO.Path.GetFullPath(pathStop);
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

        public List<StopCsv> GetAllStops(ProviderCsv provider)
        {
            // stops of chosen society
            string pathStop = $"GtfsData\\{provider}\\gtfs\\stops.txt";
            string fullPathStop = System.IO.Path.GetFullPath(pathStop);
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
            catch (DirectoryNotFoundException)
            {
                logger.Info("Something went wrong with the {0} directory (missing gtfs)", provider);
                throw;
            }
        }
        public List<CalendarCsv> GetAllCalendars(ProviderCsv provider)
        {
            // Calendar of chosen society
            string pathCalendar = $"GtfsData\\{provider}\\gtfs\\calendar.txt";
            string fullPathCalendar = System.IO.Path.GetFullPath(pathCalendar);
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
        }

        public List<RouteCsv> GetAllRoutes(ProviderCsv provider)
        {
            // routes of chosen society
            string fullPathRoute = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\routes.txt");
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

        public List<TripCsv> GetAllTrips(ProviderCsv provider)
        {
            // Trips of chosen society
            string fullPathTrip = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\trips.txt");
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
    }
}