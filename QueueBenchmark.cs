namespace SytyRouting.Model
{
    public class QueueBenchmark
    {
        public int Id;
        public int PendingElements;
        public int ProcessedElements;
        public double ProcessingRate;
        public string? ElapsedTime;
        public string? ExpectedCompletionTime;
    }
}