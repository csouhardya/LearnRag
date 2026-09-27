using Azure.AI.OpenAI;
using Azure.Search.Documents.Models;
using LearnRag.Interfaces;

namespace LearnRag.Services
{
    /// <summary>
    /// Uses Azure OpenAI to generate vector embeddings for document chunks and user queries.
    /// </summary>
    public class EmbeddingService(AzureOpenAIClient azOpenAiClient, IConfiguration config, ILogger<EmbeddingService> logger): IEmbeddingService
    {
        private readonly AzureOpenAIClient _azOpenAiClient = azOpenAiClient;
        private readonly string embeddingDeployment = config["AzureAiSearch:DeploymentName"]!;
        private readonly ILogger<EmbeddingService> _logger = logger;

        /// <summary>
        /// Generates an embedding for each chunk and creates search documents containing the text and its vector.
        /// </summary>
        /// <param name="docName">Name used to identify the source document in the search index.</param>
        /// <param name="chunks">Text chunks to embed.</param>
        /// <returns>Search documents ready to upload to Azure AI Search.</returns>
        public async Task<List<SearchDocument>> EmbedAsync(string docName, List<string> chunks)
        {
            docName = docName.Split('.')[0];
            _logger.LogInformation($"Starting embedding for doc {docName}");
            var embClient = _azOpenAiClient.GetEmbeddingClient(embeddingDeployment);
            _logger.LogInformation($"Received embedding client");

            var doc = new List<SearchDocument>();
            foreach (int i in Enumerable.Range(1, chunks.Count))
            {
                _logger.LogInformation($"Starting embedding for chunk {i}/{chunks.Count}");

                var embedding = await embClient.GenerateEmbeddingAsync(chunks[i-1]);
                var floatArr = embedding.Value.ToFloats().ToArray();
                _logger.LogInformation($"Embedding completed for chunk {i}");

                doc.Add(new()
                {
                    ["id"] = $"{docName}-chunk-{i}",
                    ["docName"] = docName,
                    ["content"] = chunks[i-1],
                    ["contentVector"] = floatArr
                });
                _logger.LogInformation($"chunk {i} Vector ==> {string.Join(",",floatArr)}");

            }
            return doc;
        }

        /// <summary>
        /// Generates an embedding vector for a user query.
        /// </summary>
        /// <param name="query">Text of the query to embed.</param>
        /// <returns>The query embedding as an array of single-precision floating-point values.</returns>
        public async Task<float[]> EmbedAsync(string query)
        {
            _logger.LogInformation($"Starting embedding of query: -> {query}");

            var embClient = _azOpenAiClient.GetEmbeddingClient(embeddingDeployment);
            _logger.LogInformation($"Received embedding client");

            var embQuery = await embClient.GenerateEmbeddingAsync(query);
            _logger.LogInformation($"Embedding completed of query");

            var vector = embQuery.Value.ToFloats().ToArray();
            _logger.LogInformation($"User query to vector -> [ {string.Join(",", vector)}]");

            return vector;
        }
    }
}
