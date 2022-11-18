namespace SytyRouting
{
    public sealed class Settings
    {
        public string GraphFileName {get; set;} = null!;
    }

    public sealed class DbSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string LocalConnectionString { get; set; } = null!;
        public string ConfigurationTableName {get; set;} = null!;
        public string PersonaTableName {get; set;} = null!;
        public string RouteTableName {get; set;} = null!;
        public string EdgeTableName {get; set;} = null!;
    }

    public sealed class DataGtfsSettings
    {
        public string [] GtfsProviders {get;set;}=null!;
        public Uri [] GtfsUris {get;set;}=null!;
    }


    public sealed class TransportSettings
    {
        public TransportMode[] TransportModes {get; set;} = null!;
        public string PublicTransportGroup {get; set;} = null!;
         public GtfsTypeToTransportModes[]GtfsTypeToTransportModes {get;set;}=null!;
        public OSMTagToTransportMode[] OSMTagsToTransportModes  {get; set;} = null!;
        public TransportModeRoutingRule[] TransportModeRoutingRules  {get; set;} = null!;
    }
    
    public sealed class TransportMode
    {
        public string Name {get; set;} = null!;
        public int MaxSpeedKmPerH {get; set;}
        public bool IsPublic {get; set;}
    }

    public sealed class TransportModeRoutingRule
    {
        public string[] CurrentTransportModes {get; set;} = null!;
        public string[] AlternativeTransportModes {get; set;} = null!;
    }

    public sealed class GtfsTypeToTransportModes
    {
        public int RouteType { get; set; }
        public string[] AllowedTransportModes {get;set;}=null!;
    }

    public sealed class OSMTagToTransportMode
    {
        public string TagKey {get; set;} = null!;
        public string TagValue {get; set;} = null!;
        public int TagId {get; set;}
        public string[] AllowedTransportModes {get; set;} = null!;
    }

    public sealed class RoutingSettings
    {
        public int MonitorSleepMilliseconds {get; set;}
        public int DBPersonaLoadAsyncSleepMilliseconds {get; set;}
        public int InitialDataLoadSleepMilliseconds {get; set;}
        public int RegularRoutingTaskBatchSize {get; set;}
    }
}
