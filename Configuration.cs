using Microsoft.Extensions.Configuration;
using NLog;

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
        public static OSMTagToTransportModes[] OSMTagsToTransportModes {get;} = null!;


        static Configuration()
        {
            // Build a config object, using env vars and JSON providers.
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

            TransportSettings transportSettings = config.GetRequiredSection("TransportSettings").Get<TransportSettings>();
            TransportModeNames= ValidateTransportModeNames(transportSettings.TransportModeNames);
            OSMTagsToTransportModes = transportSettings.OSMTagsToTransportModes;
        }

        static string[] ValidateTransportModeNames(string[] configTransportModeNames)
        {
            string defaultTransportMode = "None";
            string[] validTransportModeNames =  new string[Constants.MaxNumberOfTransportModes+1];
            validTransportModeNames[0] = defaultTransportMode;
            try
            {
                var transportModeNames = configTransportModeNames.ToList().Distinct().ToArray();
                
                if(transportModeNames.Length < Constants.MaxNumberOfTransportModes)
                {
                    Array.Resize(ref validTransportModeNames, transportModeNames.Length + 1);
                }
                else
                {
                    logger.Info("The number of transport modes in the config file should be limited to {0}. Ignoring the last {1} transport mode(s) in the list.", Constants.MaxNumberOfTransportModes, configTransportModeNames.Length - Constants.MaxNumberOfTransportModes);
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

            return validTransportModeNames;
        }
    }
}