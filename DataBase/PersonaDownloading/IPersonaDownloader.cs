using SytyRouting.Model;

namespace SytyRouting.DataBase
{
    public interface IPersonaDownloader
    {   
        void Initialize(Graph graph, string connectionString, string personaTable);
        int[] GetBatchSizes(int regularBatchSize, int elementsToProcess);
        int[] GetBatchPartition(int regularSlice, int whole, int numberOfSlices);
        int GetValidationErrors();
        Task<Persona[]> DownloadPersonasAsync(string connectionString, string personaTable, int batchSize, int offset);
    }
}