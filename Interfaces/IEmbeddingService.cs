using Azure.Search.Documents.Models;

namespace LearnRag.Interfaces
{
    public interface IEmbeddingService
    {
        Task<List<SearchDocument>> EmbedAsync(string docName, List<string> chunks);
        Task<float[]> EmbedAsync(string query);
    }
}
