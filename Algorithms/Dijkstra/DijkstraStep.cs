using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.Dijkstra
{
    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep;
        [NotNull]
        public Node ActiveNode = null!;
        public double CumulatedCost;
        public StepDirection Direction;
        public int TransportSequenceIndex;
        public byte InboundTransportMode;
        //debug: public int OutboundRouteType;
        public int InboundRouteType;
        //
    }
}