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
        public string ConfigurationTable {get; set;} = null!;
        public string PersonaTable {get; set;} = null!;
        public string PersonaRouteTable {get; set;} = null!;
        public string PersonaRouteAuxTable {get; set;} = null!;
        public string PKConstraintSuffix {get; set;} = null!;
        public string FKConstraintSuffix {get; set;} = null!;
        public string AuxiliaryTableSuffix {get; set;} = null!;
        public string RouteTable {get; set;} = null!;
        public string EdgeTable {get; set;} = null!;
        public string RoutingBenchmarkTable {get; set;} = null!;
        public string RoutingBenchmarkTempTable {get; set;} = null!;
    }

    public sealed class DataGtfsSettings
    {
        public string [] GtfsProviders {get;set;}=null!;
        public Uri [] GtfsUris {get;set;}=null!;
        public string? SelectedDate {get;set;}=null!;
    }

    public sealed class GtfsData
    {
        public Uri Uri {get;set;}=null!;
        public string ZipFile {get;set;}=null!;
    }

    public sealed class RoutingBenchmarkSettings
    {
        public RoutingProbe[] RoutingProbes {get; set;} = null!;
        public int AdditionalProbes {get; set;}
        public string[] DefaultBenchmarkSequence {get; set;} = null!;
    }

    public sealed class RoutingProbe
    {
        public string HomeLocation{get; set;} = null!;
        public double HomeLongitude {get; set;}
        public double HomeLatitude {get; set;}
        public string WorkLocation{get; set;} = null!;
        public double WorkLongitude {get; set;}
        public double WorkLatitude {get; set;}
        public string[] TransportSequence {get; set;} = null!;
    }

    public sealed class TransportSettings
    {
        public TransportMode[] TransportModes {get; set;} = null!;
        public string[] DefaultTransportSequence {get; set;} = null!;
        public string PublicTransportGroup {get; set;} = null!;
        public GtfsTypeToTransportModes[] GtfsTypeToTransportModes {get;set;}=null!;
        public OSMTags[] OSMTags  {get; set;} = null!;
        public TransportModeRoutingRule[] TransportModeRoutingRules  {get; set;} = null!;
        public double NotNullDistanceFootTransitions {get; set;}
    }
    
    public sealed class TransportMode
    {
        public string Name {get; set;} = null!;
        public int MaxSpeedKmPerH {get; set;}
        public bool IsPublic {get; set;}
        public double RoutingPenalty {get; set;}
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
        public double RoutingPenalty {get; set;}
    }

    public sealed class OSMTags
    {
        public string Key {get; set;} = null!;
        public string Value {get; set;} = null!;
        public int Id {get; set;}
        public string[] AllowedTransportModes {get; set;} = null!;
        public double RoutingPenalty {get; set;}
    }

    public sealed class RoutingSettings
    {
        public int MonitorSleepMilliseconds {get; set;}
        public int DBPersonaLoadAsyncSleepMilliseconds {get; set;}
        public int InitialDataLoadSleepMilliseconds {get; set;}
        public int RegularRoutingTaskBatchSize {get; set;}
        public string DefaultRouteStartDateTime {get; set;} = null!;
    }
}
