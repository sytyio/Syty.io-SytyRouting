namespace SytyRouting
{

    public class DijkstraStep
    {
        public DijkstraStep? PreviousStep { get; set; }
        public Node? TargetNode { get; set; }
        public double CumulatedCost { get; set; }
    }
}