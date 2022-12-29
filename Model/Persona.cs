using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{

    public class Persona
    {
        public int Id;
        public Point? HomeLocation;
        public Point? WorkLocation;
        public LineString? Route;
        public byte[] RequestedTransportSequence = null!;
        public Dictionary<int, Tuple<byte,int>>? TransportModeTransitions;
        public Tuple<string[],DateTime[]> TTextTransitions = null!;
        public bool SuccessfulRouteComputation;
    }
}