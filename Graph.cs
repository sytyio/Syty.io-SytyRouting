using Npgsql;
namespace SytyRouting
{
    public class Graph
    {
        public async Task DBLoadAsync()
        {
            var connectionString = Constants.connectionString;
            string queryString;

            List<Node> nodes = new List<Node>();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Preliminary test to read the first 100 'ways' rows and display the retrieved Node data
            queryString = "SELECT * FROM public.ways ORDER BY source ASC LIMIT 100";
            Console.WriteLine("");
            Console.WriteLine(queryString);
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                long nodeId;
                double nodeX;
                double nodeY;

                while (await reader.ReadAsync())
                {
                    nodeId = Convert.ToInt64(reader.GetValue(6));
                    nodeX = Convert.ToDouble(reader.GetValue(17));
                    nodeY = Convert.ToDouble(reader.GetValue(18));
                    
                    Console.WriteLine("Query result: node id:{0}, x={1}, y={2}", nodeId, nodeX, nodeY);
                    if(nodes.Exists(n => n.Id == nodeId))
                    {
                        Console.WriteLine("The node {0} is already in the List", nodeId);
                    }
                    else
                    {
                        var newNode = this.CreateNode(nodeId, nodeX, nodeY);
                        nodes.Add(newNode);
                    }
                }
            }
        }

        private Node CreateNode(long id, double x, double y)
        {
            var node = new Node{Id = id, X = x, Y = y};
            return node;
        }

    }
}