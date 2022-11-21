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
        public int TransportModePlaceholder;
        public byte TransportMode;
    }
}