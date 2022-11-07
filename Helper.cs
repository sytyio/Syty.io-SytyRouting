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

        // Masks 

        public static async Task<Dictionary<int,byte>> CreateMappingTagIdToTransportMode(Dictionary<String,byte> transportModeMasks)
        {
            Dictionary<int,byte> tagIdToTransportMode = await Configuration.CreateMappingTagIdToTransportMode(transportModeMasks);

            foreach(var ti2tmm in tagIdToTransportMode)
            {
                Console.WriteLine("{0}: {1} :: {2}", ti2tmm.Key,ti2tmm.Value,TransportModesToString(ti2tmm.Value));
            }
            return tagIdToTransportMode;
        }

        public static Dictionary<int,byte> CreateMappingRouteTypeToTransportMode(Dictionary<String,byte> transportModeMasks){
            Dictionary<int,byte> routeTypeToTransportMode= Configuration.CreateMappingTypeRouteToTransportMode(transportModeMasks);
                        foreach(var rt2tmm in routeTypeToTransportMode)
            {
                logger.Info("{0}: {1} :: {2}", rt2tmm.Key,rt2tmm.Value,TransportModesToString(rt2tmm.Value));
            }
            return routeTypeToTransportMode;
        }

        public static Dictionary<String,byte> CreateTransportModeMasks(string[] transportModes)
        {
            var transportModeMasks = new Dictionary<string, byte>();
            // Create bitmasks for the Transport Modes based on the configuration data using a Dictionary.
            try
            {
                transportModeMasks.Add(transportModes[0],0);
                for(int n = 0; n < transportModes.Length-1; n++)
                {
                    var twoToTheNth = (byte)Math.Pow(2,n);
                    var transportName = transportModes[n+1];
                    transportModeMasks.Add(transportName,twoToTheNth);
                }
            }
            catch (Exception e)
            {
                logger.Info("Transport Mode bitmask creation error: {0}", e.Message);
            }
            return transportModeMasks;
        }

        public static string TransportModesToString(int transportModes)
        {
            var transportModeMasks = Helper.CreateTransportModeMasks(Configuration.TransportModeNames);
            string result = "";
            foreach(var tmm in transportModeMasks)
            {
                if(tmm.Value != 0 && (transportModes & tmm.Value) == tmm.Value)
                {
                    result += tmm.Key + " ";
                }
            }
                
            return (result == "")? Constants.DefaulTransportMode : result;
        }
        
        public static byte GetTransportModeMask(string transportModeName)
        {
            var transportModeMasks = Helper.CreateTransportModeMasks(Configuration.TransportModeNames);
            if(transportModeMasks.ContainsKey(transportModeName))
            {
                return transportModeMasks[transportModeName];
            }
            else
            {
                logger.Info("Transport mode name {0} not found in the validated list of transport modes. (Transport configuration file.)", transportModeName);
                return 0;
            }
        }

        public static byte GetTransportModes(int tagId,Dictionary<int,byte> tagIdToTransportMode)
        {
            if (tagIdToTransportMode.ContainsKey(tagId))
            {
                return tagIdToTransportMode[tagId];
            }
            else
            {
                logger.Info("Unable to find OSM tag_id {0} in the tag_id-to-Transport Mode mapping. Transport Mode set to 'None'", tagId);
                return (byte)0; // Default Ttransport Mode: 0 ("None");
            }
        }

    }
}



