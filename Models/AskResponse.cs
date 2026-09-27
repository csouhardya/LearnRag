using OpenAI.Chat;

namespace LearnRag.Models
{
    public class AskResponse
    {
        public string Response { get; set; }
        public string Model { get; set; }
        public ChatTokenUsage? Usage { get; set; }
        public ChatMessageRole? Role { get; set; }
        public IReadOnlyList<ChatToolCall>? Tools { get; set; }
    }
}
