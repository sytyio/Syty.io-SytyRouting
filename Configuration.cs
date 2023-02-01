using System.Globalization;
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
        public static string ConfigurationTable {get;}
        public static string PersonaTable {get;}
        public static string PersonaRouteTable {get;}
        public static string PersonaRouteAuxTable {get;}
        public static string AuxiliaryTableSuffix {get;}
        public static string ComputedRouteTable {get;}
        public static string EdgeTable {get;}
        public static string RoutingBenchmarkTable {get;}
        public static string RoutingBenchmarkTempTable {get;}

        // Gtfs settings : 
        public static DataGtfsSettings DataGtfsSettings {get;set;} = null!;
        public static Dictionary<String, Uri> ProvidersInfo {get;set;} = null!;

        public static string SelectedDate {get;set;}=null!;

        // Routing parameters:
        public static int MonitorSleepMilliseconds {get;}
        public static int DBPersonaLoadAsyncSleepMilliseconds {get;}
        public static int InitialDataLoadSleepMilliseconds {get;}
        public static int RegularRoutingTaskBatchSize {get;}
        public static DateTime DefaultRouteStartTime {get;}

        // Routing benchmark:
        public static RoutingProbe[] RoutingProbes {get;}
        public static int AdditionalRoutingProbes {get;}
        public static string[] DefaultBenchmarkSequence {get;}

        // Transport parameters:
        public static Dictionary<int,string> TransportModeNames {get;}
        public static string[] PublicTransportModes {get;}
        public static string PublicTransportGroup {get;} = null!;
        public static string[] DefaultTransportSequence {get;} = null!;
        public static Dictionary<int,double> TransportModeSpeeds {get;}
        public static Dictionary<int,double> TransportModeRoutingPenalties {get;}
        public static OSMTags[] OSMTags {get;} = null!;
        public static GtfsTypeToTransportModes [] GtfsTypeToTransportModes {get;}=null!;
        public static double NotNullDistanceFootTransitions;
        private static TransportSettings? transportSettings {get; set;} = null!;

        // Transport Modes routing rules:
        public static TransportModeRoutingRule[] TransportModeRoutingRules {get;} = null!;
        

        static Configuration()
        {
            // Build a config object, using JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.data.json")
                .AddJsonFile("appsettings.routing.json")
                .AddJsonFile("appsettings.transport.json")
                .AddJsonFile("appsettings.routingbenchmark.json")
                .Build();

            // Get values from the config representation given their key and their target type:
            if(config is not null)
            {
                Settings? settings = config.GetRequiredSection("Settings").Get<Settings>();
                if(settings != null)
                    GraphFileName = settings.GraphFileName;
                else
                    throw new Exception("Config settings are not valid.");

                DbSettings? dBConnectionSettings = config.GetRequiredSection("DbConnectionSettings").Get<DbSettings>();
                if(dBConnectionSettings != null)
                    ConnectionString = dBConnectionSettings.ConnectionString;
                else
                    throw new Exception("Config dBConnectionSettings are not valid.");
                LocalConnectionString = dBConnectionSettings.LocalConnectionString;
                
                DbSettings? dBTableSettings = config.GetRequiredSection("DbTableSettings").Get<DbSettings>();
                if(dBTableSettings != null)
                    ConfigurationTable = dBTableSettings.ConfigurationTable;
                else
                    throw new Exception("Config dBTableSettings are not valid.");
                
                PersonaTable = dBTableSettings.PersonaTable;
                PersonaRouteTable = dBTableSettings.PersonaRouteTable;
                PersonaRouteAuxTable = dBTableSettings.PersonaRouteAuxTable;
                AuxiliaryTableSuffix = dBTableSettings.AuxiliaryTableSuffix;
                ComputedRouteTable = dBTableSettings.RouteTable;
                EdgeTable = dBTableSettings.EdgeTable;
                RoutingBenchmarkTable = dBTableSettings.RoutingBenchmarkTable;
                RoutingBenchmarkTempTable = dBTableSettings.RoutingBenchmarkTempTable;

                DataGtfsSettings? dataGtfsSettings = config.GetRequiredSection("DataGtfsSettings").Get<DataGtfsSettings>();
                if(dataGtfsSettings != null)
                    CreateDictionaryProviderUri(dataGtfsSettings.GtfsProviders,dataGtfsSettings.GtfsUris);
                else
                    throw new Exception("Config dataGtfsSettings are not valid.");
                InitialiseDateGtfs(dataGtfsSettings.SelectedDate);

                RoutingSettings? routingSettings = config.GetRequiredSection("RoutingSettings").Get<RoutingSettings>();
                if(routingSettings != null)
                    MonitorSleepMilliseconds = routingSettings.MonitorSleepMilliseconds;
                else
                    throw new Exception("Config routingSettings are not valid.");
                
                DBPersonaLoadAsyncSleepMilliseconds = routingSettings.DBPersonaLoadAsyncSleepMilliseconds;
                InitialDataLoadSleepMilliseconds = routingSettings.InitialDataLoadSleepMilliseconds;
                RegularRoutingTaskBatchSize = routingSettings.RegularRoutingTaskBatchSize;
                DefaultRouteStartTime = ValidateTimeStamp(routingSettings.DefaultRouteStartTime);

                RoutingBenchmarkSettings? routingBenchmarkSettings = config.GetRequiredSection("RoutingBenchmarkSettings").Get<RoutingBenchmarkSettings>();
                if(routingBenchmarkSettings != null)
                    RoutingProbes = routingBenchmarkSettings.RoutingProbes;
                else
                    throw new Exception("Config routingBenchmarkSettings are not valid.");
                
                AdditionalRoutingProbes = routingBenchmarkSettings.AdditionalProbes;
                DefaultBenchmarkSequence = routingBenchmarkSettings.DefaultBenchmarkSequence;

                transportSettings = config.GetRequiredSection("TransportSettings").Get<TransportSettings>();
                if(transportSettings != null)
                    PublicTransportGroup = transportSettings.PublicTransportGroup;
                else
                    throw new Exception("Config transportSettings are not valid.");
            
                TransportModeNames = ValidateTransportModeNames(transportSettings.TransportModes);
                PublicTransportModes = ValidatePublicTransportModes(transportSettings.TransportModes);
                TransportModeSpeeds = ValidateTransportModeSpeeds(transportSettings.TransportModes);
                TransportModeRoutingPenalties = ValidateTransportModeRoutingPenalties(transportSettings.TransportModes);
                DefaultTransportSequence = transportSettings.DefaultTransportSequence;
                TransportModeRoutingRules = transportSettings.TransportModeRoutingRules;
                OSMTags = transportSettings.OSMTags;
                GtfsTypeToTransportModes = transportSettings.GtfsTypeToTransportModes;
                NotNullDistanceFootTransitions = transportSettings.NotNullDistanceFootTransitions;
            }
            else
            {
                throw new Exception("Invalid configuration data.");
            }
        }


        public static DateTime ValidateTimeStamp(string timeStamp)
        {
            DateTime timeStampResult;
            if (DateTime.TryParse(timeStamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStampResult))
                return timeStampResult;
            else
                return Constants.BaseDateTime;
        }



        public static Dictionary<int,byte> CreateMappingTypeRouteToTransportMode(Dictionary<int,byte> transportModeMasks)
        {
            int [] tagsGtfs = Configuration.GtfsTags();
            Dictionary<int,byte> routeTypeToTransportMode= new Dictionary<int, byte>();
            for (var i=0;i<tagsGtfs.Length;i++){
                byte mask = 0;
                var configAllowedTransportModes=Configuration.GtfsTypeToTransportModes[i].AllowedTransportModes;
                foreach(var transportName in configAllowedTransportModes)
            {
                var key = TransportModes.NameToIndex(transportName);
                if(transportModeMasks.ContainsKey(key))
                {
                    mask |= transportModeMasks[key];
                }
                else
                {
                    logger.Info("Transport Mode '{0}' not found.",transportName);
                }
            }
            if (!routeTypeToTransportMode.ContainsKey(tagsGtfs[i]))
            {
                routeTypeToTransportMode.Add(tagsGtfs[i], mask);
            }
            else
            {
                logger.Debug("Unable to add key to OSM-tag_id - to - Transport-Mode mapping. Tag id: {0}", tagsGtfs[i]);
            }
            }
            return routeTypeToTransportMode;
        }

        public static bool VerifyTransportListFromGraphFile(string[] transportModes)
        {
            var transportModeNamesArray = TransportModeNames.Values.ToArray();
            if(transportModes.Length == transportModeNamesArray.Length)
            {
                for(int i = 0; i < transportModeNamesArray.Length; i++)
                {
                    if(!transportModes[i].Equals(transportModeNamesArray[i]))
                        return false;
                }
                return true;
            }

            return false;            
        }

        private static Dictionary<int,string> ValidateTransportModeNames(TransportMode[] configTransportModes)
        {
            string[] configTransportModeNames =  new string[configTransportModes.Length];
            for(int i=0; i<configTransportModeNames.Length; i++)
            {
                configTransportModeNames[i]=configTransportModes[i].Name;
            }

            Dictionary<int,string> validTransportModeNames =  new Dictionary<int,string>(0);
            validTransportModeNames[0] = TransportModes.NoTransportMode;
            try
            {
                var transportModeNames = configTransportModeNames.ToList().Distinct().ToArray();
                
                if(transportModeNames.Length > TransportModes.MaxNumberOfTransportModes)
                {
                    Array.Resize(ref transportModeNames, TransportModes.MaxNumberOfTransportModes);
                    logger.Info("The number of transport modes in the configuration file should be limited to {0}. Ignoring the last {1} transport mode(s) in the list.", TransportModes.MaxNumberOfTransportModes, configTransportModeNames.Length - TransportModes.MaxNumberOfTransportModes);
                }

                int index=1;
                for(;index <= transportModeNames.Length; index++)
                {
                    validTransportModeNames[index] = transportModeNames[index-1]; 
                }
            }
            catch(Exception e)
            {
                logger.Debug("Configuration error. Transport mode names: {0}", e.Message);
            }

            string transportModesString = TransportModes.NamesToString(validTransportModeNames.Values.ToArray()[1..]);
            logger.Info("Transport Modes loaded: {0}.", transportModesString);

            return validTransportModeNames;
        }

        private static string[] ValidatePublicTransportModes(TransportMode[] transportModes)
        {
            var validPublicTransportModes = new List<string>(0);
            foreach(TransportMode transportMode in transportModes)
            {                
                if(TransportModeNames.ContainsValue(transportMode.Name) && transportMode.IsPublic==true)
                {
                    validPublicTransportModes.Add(transportMode.Name);
                }
            }
            return validPublicTransportModes.ToArray();
        }

        private static Dictionary<int,double> ValidateTransportModeSpeeds(TransportMode[] transportModes)
        {            
            Dictionary<int,double> validTransportModeSpeeds = new Dictionary<int,double>(TransportModeNames.Count);

            foreach(var transportModeName in TransportModeNames)
            {
                for(int i=0; i<transportModes.Length; i++)
                {                    
                    if(transportModeName.Value.Equals(transportModes[i].Name))
                    {
                        var key = transportModeName.Key;
                        if(!validTransportModeSpeeds.ContainsKey(key))
                        {
                            if(transportModes[i].MaxSpeedKmPerH>0)
                            {
                                validTransportModeSpeeds.Add(key, Helper.KMPerHourToMPerS(transportModes[i].MaxSpeedKmPerH));
                                break;
                            }
                            else
                            {
                                logger.Info("The maximum speed for Transport Mode '{0}' should be greater than 0.", transportModes[i].Name);
                            }
                        }
                    }
                }
            }

            return validTransportModeSpeeds;
        }

        private static Dictionary<int,double> ValidateTransportModeRoutingPenalties(TransportMode[] transportModes)
        {            
            Dictionary<int,double> validTransportModePenalties = new Dictionary<int,double>(TransportModeNames.Count);

            foreach(var transportModeName in TransportModeNames)
            {
                for(int i=0; i<transportModes.Length; i++)
                {                    
                    if(transportModeName.Value.Equals(transportModes[i].Name))
                    {
                        var key = transportModeName.Key;
                        if(!validTransportModePenalties.ContainsKey(key))
                        {
                            if(transportModes[i].RoutingPenalty>0)
                            {
                                validTransportModePenalties.Add(key, transportModes[i].RoutingPenalty);
                                break;
                            }
                            else
                            {
                                logger.Info("The routing penalty for Transport Mode '{0}' should be greater than 0.", transportModes[i].Name); 
                            }
                        }
                    }
                }
            }

            return validTransportModePenalties;
        }

        private static async Task<OSMTags[]> ValidateOSMTagToTransportModes(OSMTags[] osmTagsToTransportModes)
        {
            int[] configTagIds = await Configuration.ValidateOSMTags();

            OSMTags[] validOSMTagsToTransportModes = new OSMTags[configTagIds.Length];

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
                if(TransportModeNames.ContainsValue(transportMode))
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

        private static int[] GtfsTags(){
            int [] gtfsTagsId = new int [GtfsTypeToTransportModes.Count()];
            for(int i=0;i<GtfsTypeToTransportModes.Count();i++){
                gtfsTagsId[i]=GtfsTypeToTransportModes[i].RouteType;
            }
            return gtfsTagsId;
        }

        public static async Task<int[]> ValidateOSMTags()
        {
            var connectionString = Configuration.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            var totalDbRows = await Helper.DbTableRowCount(Configuration.ConfigurationTable, logger);
            int[] osmTagIds = new int[totalDbRows];
            // Read the 'configuration' rows and create an array of tag_ids
            //                      0
            string queryString = "SELECT tag_id FROM " + Configuration.ConfigurationTable;

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

            int configTagIdsSize = 0;
            if(transportSettings != null)
            {
                configTagIdsSize = transportSettings.OSMTags.Length;
                int[] configTagIds = new int[configTagIdsSize];
                for(int i = 0; i < transportSettings.OSMTags.Length; i++)
                {
                    configTagIds[i] = transportSettings.OSMTags[i].Id;
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
            }

            return osmTagIds;
        }

        private static void CreateDictionaryProviderUri(string [] providers, Uri [] uris){
            ProvidersInfo = new Dictionary<string, Uri>();
            if(providers.Count()!=uris.Count()){
                logger.Info("Problem with the providers data, not the same number of uris and providers");
            }else{
                for(int i=0;i<providers.Count();i++){
                    try{
                    ProvidersInfo.Add(providers[i],uris[i]);
                    }catch(ArgumentException){
                        logger.Info("Provider already there");
                    }
                }
            }
        }

        private static void InitialiseDateGtfs(string? selectedDate)
        {
            logger.Info("Date = {0}",selectedDate);
            if(selectedDate is null){
                SelectedDate= "";
            }else{
            SelectedDate= selectedDate;
            }
        }
    }
}