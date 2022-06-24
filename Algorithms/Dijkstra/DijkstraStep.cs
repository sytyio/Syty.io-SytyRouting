namespace SytyRouting.Algorithms.Dijkstra
{

    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep { get; set; }
        public Node? ActiveNode { get; set; }
        public double CumulatedCost { get; set; }
    }
}