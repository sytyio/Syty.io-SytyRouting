using NLog;
using Npgsql;
using System.Globalization;
using System.Runtime.CompilerServices;
using SytyRouting.Model;

namespace SytyRouting
{
    public class Helper
    {
        public static async Task<int> DbTableRowCount(string tableName, Logger logger)
        {
            int totalDbRows = 0;

            var connectionString = Constants.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            // connection.TypeMapper.UseNetTopologySuite();

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
            logger.Info(baseString, "", "ddd:hh:mm:ss.ms");
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
		private static double deg2rad(double deg) {
			return (deg * Math.PI / 180.0);
		}

		private static double rad2deg(double rad) {
			return (rad / Math.PI * 180.0);
		}
    }
}