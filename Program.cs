
namespace SytyRouting
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("syty.io routing engine for large scale datasets");
            var graph = new Graph();
            await graph.DBLoadAsync();
        }
    }
}