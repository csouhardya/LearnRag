using LearnRag.Interfaces;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LearnRag.Services
{
    /// <summary>
    /// Extracts text from PDF files in the configured upload folder and splits text into paragraphs.
    /// </summary>
    public class PdfHelperService(IConfiguration config, ILogger<PdfHelperService> logger): IPdfHelperService
    {
        private readonly string uploadFolder = config["Constants:UploadFolderPath"]!;
        private readonly ILogger<PdfHelperService> _logger = logger;
        /// <summary>
        /// Splits extracted text at double CRLF separators and returns the resulting paragraphs as chunks.
        /// </summary>
        /// <param name="content">Text to split into chunks.</param>
        /// <returns>A list containing each paragraph produced by the split.</returns>
        public List<string> ChunkText(string content)
        {
            string separator = "\r\n\r\n";
            List<string> chunks = [];
            var paragraphs = content.Split(separator);
            foreach (var p in paragraphs)
                chunks.Add(p);
            return chunks;
        }

        /// <summary>
        /// Reads each page of the named PDF and concatenates its extracted text with page separators.
        /// </summary>
        /// <param name="docName">Name of the PDF file within the configured upload folder.</param>
        /// <returns>The extracted text from all pages of the PDF.</returns>
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
