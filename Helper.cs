using NLog;
using Npgsql;
using System.Globalization;
using System.Runtime.CompilerServices;
using SytyRouting.Model;

namespace SytyRouting
{
    public class Helper
    {
        
        public static async Task<int> DbTableRowsCount(NpgsqlConnection connection, string tableName, Logger logger)
        {
            int totalDbRows = 0;

            var queryString = "SELECT count(*) AS exact_count FROM " + tableName;
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    totalDbRows = Convert.ToInt32(reader.GetValue(0));
                }
            }

            logger.Info("Total number of rows to process: {0}", totalDbRows);

            return totalDbRows;
        }
        
        public static void SetCreationBenchmark(long totalDbRows, long dbRowsProcessed, TimeSpan timeSpan, long timeSpanMilliseconds, Logger logger, [CallerMemberName] string callerName = "")
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var rowProcessingRate = (double)dbRowsProcessed / timeSpanMilliseconds * 1000; // Assuming a fairly constant rate
            var personasSetCreationTimeSeconds = totalDbRows / rowProcessingRate;
            var personasSetCreationTime = TimeSpan.FromSeconds(personasSetCreationTimeSeconds);

            var totalTime = Helper.FormatElapsedTime(personasSetCreationTime);

            logger.Debug("Number of DB rows already processed: {0}", dbRowsProcessed);
            logger.Debug("Row processing rate: {0} [Rows / s]", rowProcessingRate.ToString("F", CultureInfo.InvariantCulture));
            logger.Info("                                        Elapsed Time (HH:MM:S.mS) :: " + elapsedTime);
            logger.Info("{0,25} set creation time estimate (HH:MM:S.mS) :: {1}", callerName, totalTime);
        }

        public static string FormatElapsedTime(TimeSpan timeSpan)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
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