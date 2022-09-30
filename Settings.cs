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

    public sealed class TransportSettings
    {
        public string[] TransportModeNames {get; set;} = null!;
        public OSMTagToTransportModes[] OSMTagsToTransportModes  {get; set;} = null!;
    }

    public sealed class OSMTagToTransportModes
    {
        public string TagKey {get; set;} = null!;
        public string TagValue {get; set;} = null!;
        public string TagId {get; set;} = null!;
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
