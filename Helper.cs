using SytyRouting.Model;

namespace SytyRouting
{
    public class Helper
    {
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