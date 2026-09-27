namespace LearnRag.Interfaces
{
    public interface IPdfHelperService
    {
        List<string> ChunkText(string content);
        Task<string> ExtractContentAsync(string docName);
    }
}
