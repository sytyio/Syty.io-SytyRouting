namespace SytyRouting
{
    public class Edge
    {
        public long Id {get; set;}
        public double Cost {get; set;}
        public Node? EndNode {get; set;}
    }
}