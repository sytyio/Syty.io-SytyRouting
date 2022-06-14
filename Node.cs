namespace SytyRouting
{

    [Serializable]
    public class Node
    {
        public long Id {get; set;}
        public double X {get; set;}
        public double Y {get; set;}
        public List<Edge> TargetEdges {get; set;} = new List<Edge>();
    }
}