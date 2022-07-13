using System.Diagnostics.CodeAnalysis;
using SytyRouting.Model;

namespace SytyRouting.Algorithms.HeuristicDijkstra
{

    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep { get; set; }
        [NotNull]
        public Node? ActiveNode { get; set; }
        public double CumulatedCost { get; set; }
    }
}