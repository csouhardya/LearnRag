using LearnRag.Interfaces;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LearnRag.Services
{
    public class PdfHelperService(IConfiguration config, ILogger<PdfHelperService> logger): IPdfHelperService
    {
        private readonly string uploadFolder = config["Constants:UploadFolderPath"]!;
        private readonly ILogger<PdfHelperService> _logger = logger;
        public List<string> ChunkText(string content)
        {
            string separator = "\r\n\r\n";
            List<string> chunks = [];
            var paragraphs = content.Split(separator);
            foreach (var p in paragraphs)
                chunks.Add(p);
            return chunks;
        }

        public async Task<string> ExtractContentAsync(string docName)
        {
            StringBuilder sb = new();
            int pageCount = 0;
            string filePath = Path.Combine(uploadFolder, docName);
            _logger.LogInformation($"Document is available at: {filePath}");

            _logger.LogInformation($"Starting content extraction of {docName}");
            using (PdfDocument pdf = PdfDocument.Open(filePath))
            {
                foreach (var page in pdf.GetPages())
                {
                    _logger.LogInformation($"Starting extraction of Page {page.Number} of {pdf.NumberOfPages}");
                    pageCount++;
                    sb.Append(ContentOrderTextExtractor.GetText(page, addDoubleNewline: true));
                    sb.Append($"\n ------ PAGE {pageCount} ------------ \n");
                }
            }
            return sb.ToString();
        }
    }
}
