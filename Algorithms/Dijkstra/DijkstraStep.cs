using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.Dijkstra
{

    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep;
        [NotNull]
        public Node? ActiveNode;
        public double CumulatedCost;
    }
}