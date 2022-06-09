namespace SytyRouting
{
    public class EdgeRepository
    {
        public static Edge Create(long id, Node endNode)
        {
            var edge = new Edge{Id = id, EndNode = endNode};
            
            return edge;
        }
    }
}