using Microsoft.Extensions.Configuration;
using NLog;
using Npgsql;

namespace SytyRouting
{
    public static class Configuration
    { 
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Program settings:
        public static string GraphFileName {get;}

        // DB connection settings:
        public static string ConnectionString {get;}
        public static string LocalConnectionString {get;}
        
        // DB table settings:
        public static string ConfigurationTableName {get;}
        public static string PersonaTableName {get;}
        public static string ComputedRouteTableName {get;}
        public static string EdgeTableName {get;}

        // Routing parameters:
        public static int MonitorSleepMilliseconds {get;}
        public static int DBPersonaLoadAsyncSleepMilliseconds {get;}
        public static int InitialDataLoadSleepMilliseconds {get;}
        public static int RegularRoutingTaskBatchSize {get;}

        // Transport parameters:
        public static string[] TransportModeNames {get;}
        public static Dictionary<string,int> TransportModeSpeeds {get;}
        public static OSMTagToTransportModes[] OSMTagsToTransportModes {get;} = null!;
        private static TransportSettings transportSettings {get; set;}
        

        static Configuration()
        {
            // Build a config object, using JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.data.json")
                .AddJsonFile("appsettings.routing.json")
                .AddJsonFile("appsettings.transport.json")
                .Build();

            // Get values from the config representation given their key and their target type:

            Settings settings = config.GetRequiredSection("Settings").Get<Settings>();
            GraphFileName = settings.GraphFileName;

            DbSettings dBConnectionSettings = config.GetRequiredSection("DbConnectionSettings").Get<DbSettings>();
            ConnectionString = dBConnectionSettings.ConnectionString;
            LocalConnectionString = dBConnectionSettings.LocalConnectionString;
            
            DbSettings dBTableSettings = config.GetRequiredSection("DbTableSettings").Get<DbSettings>();
            ConfigurationTableName = dBTableSettings.ConfigurationTableName;
            PersonaTableName = dBTableSettings.PersonaTableName;
            ComputedRouteTableName = dBTableSettings.RouteTableName;
            EdgeTableName = dBTableSettings.EdgeTableName;

            RoutingSettings routingSettings = config.GetRequiredSection("RoutingSettings").Get<RoutingSettings>();
            MonitorSleepMilliseconds = routingSettings.MonitorSleepMilliseconds;
            DBPersonaLoadAsyncSleepMilliseconds = routingSettings.DBPersonaLoadAsyncSleepMilliseconds;
            InitialDataLoadSleepMilliseconds = routingSettings.InitialDataLoadSleepMilliseconds;
            RegularRoutingTaskBatchSize = routingSettings.RegularRoutingTaskBatchSize;

            transportSettings = config.GetRequiredSection("TransportSettings").Get<TransportSettings>();
            TransportModeNames = ValidateTransportModeNames(transportSettings.TransportModes);
            TransportModeSpeeds = ValidateTransportModeSpeeds(transportSettings.TransportModeSpeeds);
            OSMTagsToTransportModes = transportSettings.OSMTagsToTransportModes;
        }

        public static bool VerifyTransportListFromGraphFile(string[] transportModes)
        {
            if(transportModes.Length == TransportModeNames.Length)
            {
                for(int i = 0; i < TransportModeNames.Length; i++)
                {
                    if(!transportModes[i].Equals(TransportModeNames[i]))
                        return false;
                }
                return true;     
            }

            return false;            
        }

        private static string[] ValidateTransportModeNames(string[] configTransportModeNames)
        {
            string[] validTransportModeNames =  new string[TransportModes.MaxNumberOfTransportModes+1];
            validTransportModeNames[0] = TransportModes.NoTransportMode;
            try
            {
                var transportModeNames = configTransportModeNames.ToList().Distinct().ToArray();
                
                if(transportModeNames.Length <= TransportModes.MaxNumberOfTransportModes)
                {
                    Array.Resize(ref validTransportModeNames, transportModeNames.Length + 1);
                }
                else
                {
                    logger.Info("The number of transport modes in the config file should be limited to {0}. Ignoring the last {1} transport mode(s) in the list.", TransportModes.MaxNumberOfTransportModes, configTransportModeNames.Length - TransportModes.MaxNumberOfTransportModes);
                }

                for(int i = 1; i < validTransportModeNames.Length; i++)
                {
                    validTransportModeNames[i] = transportModeNames[i-1]; 
                }                
            }
            catch(Exception e)
            {
                logger.Debug("Configuration error. Transport mode names: {0}", e.Message);
            }

            string transportModesString = TransportModes.NamesToString(validTransportModeNames[1..]);
            logger.Info("Transport Modes ordered by routing priority: {0}.", transportModesString);

            return validTransportModeNames;
        }

        private static Dictionary<string,int> ValidateTransportModeSpeeds(TransportModeSpeed[] transportModeSpeeds)
        {
            Dictionary<string,int> validTransportModeSpeeds = new Dictionary<string,int>(TransportModeNames.Length);

            foreach(var transportModeSpeed in transportModeSpeeds)
            {
                if(Array.Exists(TransportModeNames, validatedTransportModeName => validatedTransportModeName.Equals(transportModeSpeed.TransportMode)))
                {
                    if(!validTransportModeSpeeds.ContainsKey(transportModeSpeed.TransportMode))
                    {
                        if(transportModeSpeed.MaxSpeedKmPerH>0)
                        {
                            validTransportModeSpeeds.Add(transportModeSpeed.TransportMode, transportModeSpeed.MaxSpeedKmPerH);
                        }
                        else
                        {
                            logger.Info("The maximum speed for Transport Mode '{0}' should be greater than 0.", transportModeSpeed.TransportMode);
                        }
                    }
                    else
                    {
                        logger.Info("The maximum speed for Transport Mode '{0}' was already set to {1} [km/h].", transportModeSpeed.TransportMode, validTransportModeSpeeds[transportModeSpeed.TransportMode]);
                    }
                }
                else
                {
                    logger.Info("Unable to find '{0}' in the validated list of transport modes. Ignoring transport mode.", transportModeSpeed.TransportMode);
                }
            }

            return validTransportModeSpeeds;
        }

        private static async Task<OSMTagToTransportModes[]> ValidateOSMTagToTransportModes(OSMTagToTransportModes[] osmTagsToTransportModes)
        {
            int[] configTagIds = await Configuration.ValidateOSMTags();

            OSMTagToTransportModes[] validOSMTagsToTransportModes = new OSMTagToTransportModes[configTagIds.Length];

            for(int i = 0; i < validOSMTagsToTransportModes.Length; i++)
            {
                validOSMTagsToTransportModes[i].AllowedTransportModes = ValidateAllowedTransportModes(osmTagsToTransportModes[i].AllowedTransportModes);
            }

            return validOSMTagsToTransportModes;
        }

        public static string[] ValidateAllowedTransportModes(string[] allowedTransportModes)
        {
            var validTransportModes = new List<string>(0);
            foreach(string transportMode in allowedTransportModes)
            {                
                if(Array.Exists(TransportModeNames, validatedTransportModeName => validatedTransportModeName.Equals(transportMode)))
                {
                    validTransportModes.Add(transportMode);
                }
                else
                {
                    logger.Info("Unable to find '{0}' in the validated list of transport modes. Ignoring transport mode.", transportMode);
                }
            }
            return validTransportModes.ToArray();
        }

        public static async Task<int[]> ValidateOSMTags()
        {
            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            var totalDbRows = await Helper.DbTableRowCount(Configuration.ConfigurationTableName, logger);
            int[] osmTagIds = new int[totalDbRows];
            // Read the 'configuration' rows and create an array of tag_ids
            //                      0
            string queryString = "SELECT tag_id FROM " + Configuration.ConfigurationTableName;

            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {    
                int tagIdIndex = 0;
                while (await reader.ReadAsync())
                {
                    var tagId = Convert.ToInt32(reader.GetValue(0)); // tag_id
                    try
                    {
                        osmTagIds[tagIdIndex] = Convert.ToInt32(tagId);
                    }
                    catch (Exception e)
                    {
                        logger.Debug("Unable to process tag_id: {0}", e.Message);
                    }
                    tagIdIndex++;
                }
            }
            Array.Sort(osmTagIds, 0, osmTagIds.Length);

            int[] configTagIds = new int[transportSettings.OSMTagsToTransportModes.Length];
            for(int i = 0; i < transportSettings.OSMTagsToTransportModes.Length; i++)
            {
                configTagIds[i] = transportSettings.OSMTagsToTransportModes[i].TagId;
            }
            Array.Sort(configTagIds, 0, configTagIds.Length);

            if(configTagIds.Length == osmTagIds.Length)
            {
                for(int i = 0; i < osmTagIds.Length; i++)
                {
                    if(osmTagIds[i] != configTagIds[i])
                    {
                        logger.Info("The OSM tag_id {0} does not match database reference.", configTagIds[i]);
                        throw new Exception("OSM tag_id mismatch error.");
                    }
                }
            }
            else
            {
                logger.Info("Inconsistent number of OSM tag_ids in the configuration file.");
            }

            return osmTagIds;
        }
    }
}