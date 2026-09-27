using System.ComponentModel.DataAnnotations;
namespace LearnRag.Models
{
    public class AskRequest
    {
        [Required]
        public string userPrompt { get; set; }
    }
}
