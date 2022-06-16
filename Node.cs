namespace SytyRouting
{
    public class Node
    {
        public long Id {get; set;}
        public double X {get; set;}
        public double Y {get; set;}
        public List<Edge> InwardEdges {get; set;} = new List<Edge>();
        public List<Edge> OutwardEdges {get; set;} = new List<Edge>();
    }
}