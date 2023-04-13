using NetTopologySuite.Geometries;
using NLog;
using Npgsql;
using System.Globalization;
using System.Runtime.CompilerServices;
using SytyRouting.Model;


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
        
        public static DataSetBenchmark DataLoadBenchmark(int totalElements, int processedElements, TimeSpan timeSpan, Logger logger, [CallerMemberName] string callerName = "")
        {
            var elapsedTime = Helper.FormatElapsedTime(timeSpan);

            var elementProcessingRate = GetProcessingRate(processedElements,timeSpan.TotalMilliseconds);
            double setCreationTimeSeconds;
            TimeSpan setCreationTime;
            if(elementProcessingRate != 0 && !double.IsNaN(elementProcessingRate))
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

        public static double GetProcessingRate(int processedElements, double totalMilliseconds)
        {
            return 1000.0 * (double)processedElements / totalMilliseconds; // Assuming a fairly constant rate
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
            if(dist>1.0)
            {
                dist = 1.0;
            }
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
                XYMPoint[] validatedGeometry = ValidateMOrdinateProgression(fullGeometry);
                NormalizeGeometry(validatedGeometry);

                var internalGeometry = new XYMPoint[validatedGeometry.Length-2];
                for(var i = 0; i < internalGeometry.Length; i++)
                {
                    internalGeometry[i] = validatedGeometry[i+1];
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

        private static XYMPoint[] ValidateMOrdinateProgression(XYMPoint[] fullGeometry)
        {
            List<XYMPoint> validatedGeometry = new List<XYMPoint>(fullGeometry.Length);
            if(fullGeometry.Length>0)
            {
                validatedGeometry.Add(fullGeometry[0]);
                for(int i = 1; i < fullGeometry.Length; i++)
                {
                    if(fullGeometry[i].M > fullGeometry[i-1].M)
                    {
                        validatedGeometry.Add(fullGeometry[i]);
                    }
                }
            }

            return validatedGeometry.ToArray();
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

        public static double KMPerHourToMPerS(double kmPerHour)
        {
			return ( kmPerHour * 1_000.0 / 60.0 / 60.0 );  // [km/h]*[1000m/1km]*[1h/60min]*[1min/60s] = [m/s]);
		}

        public static double MPerSToKMPerHour(double mPerS)
        {
			return ( mPerS / 1_000.0 * 60.0 * 60.0 );  // [m/s]*[1km/1000m]*[60min/1h]*[60s/1min] = [km/h]);
		}

        public static Boolean AreNodesAtSamePosition(Node a, Node b)
        {
            return a.X == b.X && a.Y == b.Y;
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

        public static double ComputeEdgeCost(CostCriteria costCriteria, Edge edge, byte transportMode)
        {              
            double cost = 0;                 
            switch(costCriteria)
            {
                case CostCriteria.MinimalTravelTime:
                {
                    var speed = ComputeSpeed(edge,transportMode);
                    cost =  edge.LengthM / speed;
                    break;
                }                
                case CostCriteria.MinimalTravelDistance:
                {
                    cost = edge.LengthM;
                    break;
                }
            }

            if(edge.OneWayState == OneWayState.Reversed)
            {
                cost = -1*cost;                
            }

            cost = ApplyRoutingPenalties(edge, transportMode, cost);

            return cost;
        }

        public static double ApplyRoutingPenalties(Edge edge, byte transportMode, double cost)
        {
            if(TransportModes.RouteTypesToPenalties.ContainsKey(edge.TagIdRouteType))
            {
                var routingPenalty = TransportModes.RouteTypesToPenalties[edge.TagIdRouteType];
                cost = cost * routingPenalty;
            }

            if(TransportModes.MasksToRoutingPenalties.ContainsKey(transportMode))
            {
                var routingPenalty = TransportModes.MasksToRoutingPenalties[transportMode];
                cost = cost * routingPenalty;
            }

            return cost;
        }

        public static double ComputeSpeed(Edge edge, byte transportMode)
        {
            double speed = 0;
            double transportModeSpeed = 0;

            if(TransportModes.MasksToSpeeds.ContainsKey(transportMode))
            {
                transportModeSpeed = TransportModes.MasksToSpeeds[transportMode];
                if(edge.MaxSpeedMPerS>transportModeSpeed && transportModeSpeed>0)
                {
                    speed = transportModeSpeed;
                }
                else if(edge.MaxSpeedMPerS>0)
                {
                    speed = edge.MaxSpeedMPerS;
                }
            }
            else if(edge.MaxSpeedMPerS>0)
            {
                speed = edge.MaxSpeedMPerS;
            }

            if(speed==0)
            {
                logger.Debug("Edge OSM ID: {1} :: Edge speed: {2} [m/s] :: Transport Mode: {3} :: Transport Mode: speed {4} [m/s]", edge.OsmID, edge.MaxSpeedMPerS, TransportModes.MaskToString(transportMode), transportModeSpeed);
                throw new Exception("Unable to compute cost: Segment speed is zero.");
            }

            return speed;
        }
    }
}



