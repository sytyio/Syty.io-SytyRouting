namespace SytyRouting
{
    public class NodeRepository
    {
        public static Node Create(long id, double x, double y)
        {
            var node = new Node{Id = id, X = x, Y = y};
            
            return node;
        }
    }
}