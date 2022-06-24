namespace SytyRouting
{

    public class DijkstraStep
    {
        public long Id { get; set; }
        public DijkstraStep? PreviousStep { get; set; }
        public Node? TargetNode { get; set; }
        public double CumulatedCost { get; set; }
    }
}