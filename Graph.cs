using Npgsql;
namespace SytyRouting
{
    public class Graph
    {
        public async Task DBLoadAsync()
        {
            var connectionString = Constants.connectionString;
            string queryString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Preliminary test to read the first 100 'ways' rows and display the retrieved Node data
            queryString = "SELECT * FROM public.ways ORDER BY gid ASC LIMIT 100";
            Console.WriteLine("");
            Console.WriteLine(queryString);
            await using (var command = new NpgsqlCommand(queryString, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    Console.WriteLine("Query result: node id:{0}, x={1}, y={2}", reader.GetValue(6),reader.GetValue(17),reader.GetValue(18));
                }
            }
        }
    }
}