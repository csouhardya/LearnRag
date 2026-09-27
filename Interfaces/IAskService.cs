using LearnRag.Models;

namespace LearnRag.Interfaces
{
    public interface IAskService
    {
        Task<AskResponse> AskAsync(string question);
    }
}
