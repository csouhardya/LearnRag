namespace LearnRag.Interfaces
{
    public interface IIngestionService
    {
        Task<int> IngestDocumentAsync(string docName);
    }
}
