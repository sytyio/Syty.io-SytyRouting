using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{

    public class Persona
    {
        public int Id;
        public Point? HomeLocation;
        public Point? WorkLocation;
        public LineString? Route;
        public byte[]? requestedTransportSequence;
        public byte[]? definiteTransportSequence;
        public Dictionary<int, byte>? TransportModeTransitions;
        public bool SuccessfulRouteComputation;
    }
}