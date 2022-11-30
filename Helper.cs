using NetTopologySuite.Geometries;
using NLog;
using Npgsql;
using System.Globalization;
using System.Runtime.CompilerServices;
using SytyRouting.Model;
using NetTopologySuite.LinearReferencing;


namespace SytyRouting
{
    public class Helper
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static async Task<int> DbTableRowCount(string tableName, Logger logger)
        {
            int totalDbRows = 0;

            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var queryString = "SELECT count(*) AS exact_count FROM " + tableName;
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    totalDbRows = Convert.ToInt32(reader.GetValue(0));
                }
            }
            await connection.CloseAsync();

            logger.Info("Total number of rows to process: {0}", totalDbRows);

            return totalDbRows;
        }
        
        public static DataSetBenchmark DataLoadBenchmark(int totalElements, int processedElements, TimeSpan timeSpan, long timeSpanMilliseconds, Logger logger, [CallerMemberName] string callerName = "")
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var elementProcessingRate = (double)processedElements / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            double setCreationTimeSeconds;
            TimeSpan setCreationTime;
            if(elementProcessingRate != 0)
            {
                setCreationTimeSeconds = totalElements / elementProcessingRate;
                setCreationTime = TimeSpan.FromSeconds(setCreationTimeSeconds);
            }
            else
            {
                setCreationTime = TimeSpan.MaxValue;
            }

            var totalTime = Helper.FormatElapsedTime(setCreationTime);

            logger.Info("{0}:", callerName);
            logger.Info("Number of elements already processed: {0}", processedElements);
            logger.Info("Element processing rate: {0} [Elements / s]", elementProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            string baseString = "{0,48} :: {1,-25}";
            logger.Info(baseString, "", "d.hh:mm:ss.ms");
            logger.Info(baseString, "Elapsed Time", elapsedTime);
            logger.Info(baseString, "Data set creation time estimate", totalTime);
            logger.Info("");

            return new DataSetBenchmark {PendingElements = totalElements - processedElements, ProcessedElements = processedElements, ProcessingRate = elementProcessingRate, ElapsedTime = elapsedTime, ExpectedCompletionTime = totalTime};
        }

        public static string FormatElapsedTime(TimeSpan timeSpan)
        {
            // Format: [-][d.]hh:mm:ss[.fffffff]
            string elapsedTime = String.Format("{0:0}.{1:00}:{2:00}:{3:00}.{4:000}",
                timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds);

            return elapsedTime;
        }

        public static double GetSquaredDistance(double x1, double y1, double x2, double y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        public static double GetSquaredDistance(Node n, double x, double y)
        {
            return GetSquaredDistance(n.X, n.Y, x, y);
        }

        public static double GetSquaredDistance(Node n, Node m)
        {
            return GetSquaredDistance(n.X, n.Y, m.X, m.Y);
        }

        public static double GetDistance(Node n, Node m)
        {
            return GetDistance(n.X, n.Y, m.X, m.Y);
        }

        public static double GetDistance(double lon1, double lat1, double lon2, double lat2) 
        {
			double theta = lon1 - lon2;
			double dist = Math.Sin(deg2rad(lat1)) * Math.Sin(deg2rad(lat2)) + Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) * Math.Cos(deg2rad(theta));
			dist = Math.Acos(dist);
			dist = rad2deg(dist);
			dist = dist * 110574.2727;
			return (dist);
		}

        public static XYMPoint[]? GetInternalGeometry(LineString geometry, OneWayState oneWayState)
        {
            if(geometry.Count > 2)
            {
                Coordinate[] coordinates = geometry.Coordinates;
                            
                if(oneWayState == OneWayState.Reversed)
                    coordinates = coordinates.Reverse().ToArray();
                
                var fullGeometry = new XYMPoint[coordinates.Length];
                
                for(int c = 0; c < coordinates.Length; c++)
                {
                    XYMPoint xYMPoint;

                    xYMPoint.X = coordinates[c].X;
                    xYMPoint.Y = coordinates[c].Y;
                    xYMPoint.M = 0;

                    fullGeometry[c] = xYMPoint;
                }

                CalculateCumulativeDistance(fullGeometry, fullGeometry.Length-1);
                NormalizeGeometry(fullGeometry);

                var internalGeometry = new XYMPoint[coordinates.Length-2];
                for(var i = 0; i < internalGeometry.Length; i++)
                {
                    internalGeometry[i] = fullGeometry[i+1];
                }
                
                return internalGeometry;
            }
            else
            {
                return null;
            }
        }

        private static double CalculateCumulativeDistance(XYMPoint[] internalGeometry, int index)
        {
            while(index > 0 && index < internalGeometry.Length)
            {
                var x1 = internalGeometry[index].X;
                var y1 = internalGeometry[index].Y;
                var x2 = internalGeometry[index-1].X;
                var y2 = internalGeometry[index-1].Y;
                var distance = Helper.GetDistance(x1, y1, x2, y2);

                var cumulativeDistance = distance + CalculateCumulativeDistance(internalGeometry, index-1);

                internalGeometry[index].M = cumulativeDistance;

                return cumulativeDistance;
            }
            return 0;
        }

        private static void NormalizeGeometry(XYMPoint[] geometry)
        {
            double normalizationParameter = geometry.Last().M;
            for(int g = 0; g < geometry.Length; g++)
            {
                geometry[g].M = geometry[g].M / normalizationParameter;
            }
        }

		private static double deg2rad(double deg) {
			return (deg * Math.PI / 180.0);
		}

		private static double rad2deg(double rad) {
			return (rad / Math.PI * 180.0);
		}

        public static Boolean AreNodesAtSamePosition(Node a, Node b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static List<LineString> SplitLineStringByPoints(LineString ls, Point[] pts, string shapeId)
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

        public static List<DateTime> GetWeekdayInRange(DateTime dateBegin, DateTime dateEnd, DayOfWeek day)
        {
            const int daysInWeek = 7;
            var result = new List<DateTime>();
            var daysToAdd = ((int)day - (int)dateBegin.DayOfWeek + daysInWeek) % daysInWeek;

            do
            {
                dateBegin = dateBegin.AddDays(daysToAdd);
                result.Add(dateBegin);
                daysToAdd = daysInWeek;
            } while (dateBegin < dateEnd);

            return result;
        }
    }
}



