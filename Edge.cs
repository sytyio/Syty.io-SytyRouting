namespace SytyRouting
{
    public class Edge
    {
        public long Id {get; set;}
        public double Cost {get; set;}
        public Node? SourceNode {get; set;}
        public Node? TargetNode {get; set;}
    }
}