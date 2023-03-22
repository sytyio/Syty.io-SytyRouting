using NetTopologySuite.Geometries;

namespace SytyRouting.Model
{

    public class Persona
    {
        public int Id;
        public Point? HomeLocation;
        public Point? WorkLocation;
        public DateTime StartDateTime;
        public LineString? Route;
        public byte[] RequestedTransportSequence = null!;
        public Dictionary<int, Tuple<byte,int>>? TransportModeTransitions; // <Node Idx, <Transport Mask, Route Type> 
        public Tuple<string[],DateTime[]> TTextTransitions = null!; // <transportModes[],timeStamps[]>
        public bool SuccessfulRouteComputation;
    }
}