using Microsoft.Extensions.Configuration;

namespace SytyRouting
{
    public static class Configuration
    { 
        // Program settings:
        public static string GraphFileName {get;}

        // DB connection settings:
        public static string ConnectionString {get;}
        public static string LocalConnectionString {get;}
        
        // DB table settings:
        public static string PersonaTableName {get;}
        public static string RouteTableName {get;}
        public static string EdgeTableName {get;}

        // Routing parameters:
        public static int MonitorSleepMilliseconds {get;}
        public static int DBPersonaLoadAsyncSleepMilliseconds {get;}
        public static int InitialDataLoadSleepMilliseconds {get;}
        public static int RegularRoutingTaskBatchSize {get;}

        static Configuration()
        {
            // Build a config object, using env vars and JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.data.json")
                .AddJsonFile("appsettings.routing.json")
                // .AddEnvironmentVariables()
                .Build();

            // Get values from the config given their key and their target type.
            Settings settings = config.GetRequiredSection("Settings").Get<Settings>();
            GraphFileName = settings.GraphFileName;

            DbSettings dBConnectionSettings = config.GetRequiredSection("DbConnectionSettings").Get<DbSettings>();
            ConnectionString = dBConnectionSettings.ConnectionString;
            LocalConnectionString = dBConnectionSettings.LocalConnectionString;
            
            DbSettings dBTableSettings = config.GetRequiredSection("DbTableSettings").Get<DbSettings>();
            PersonaTableName = dBTableSettings.PersonaTableName;
            RouteTableName = dBTableSettings.RouteTableName;
            EdgeTableName = dBTableSettings.EdgeTableName;

            RoutingSettings routingSettings = config.GetRequiredSection("RoutingSettings").Get<RoutingSettings>();
            MonitorSleepMilliseconds = routingSettings.MonitorSleepMilliseconds;
            DBPersonaLoadAsyncSleepMilliseconds = routingSettings.DBPersonaLoadAsyncSleepMilliseconds;
            InitialDataLoadSleepMilliseconds = routingSettings.InitialDataLoadSleepMilliseconds;
            RegularRoutingTaskBatchSize = routingSettings.RegularRoutingTaskBatchSize;

        }
    }
}