using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.LiveContractionDijkstra
{
    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep;
        [NotNull]
        public Node? ActiveNode;
        public double CumulatedCost;
        public StepDirection Direction;       
    }
}