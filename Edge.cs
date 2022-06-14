namespace SytyRouting
{
    [Serializable]
    public class Edge
    {
        public long Id {get; set;}
        public Node? EndNode {get; set;}
    }
}