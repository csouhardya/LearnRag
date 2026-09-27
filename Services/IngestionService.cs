using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using LearnRag.Interfaces;

namespace LearnRag.Services
{
    /// <summary>
    /// Extracts documents, creates embeddings for their text chunks, and stores those chunks in Azure AI Search.
    /// </summary>
    public class IngestionService: IIngestionService
    {
        private readonly SearchClient searchClient; // to communicate with azure ai search, uploading document, search for relevant chunk
        private readonly SearchIndexClient searchIndexClient; // to update the search index
        private readonly IConfiguration _config;
        private readonly IEmbeddingService _embeddingService;
        private readonly IPdfHelperService _pdfService;
        private readonly ILogger<IngestionService> _logger;
        private readonly string searchServiceEndpoint;
        private readonly string searchServiceApiKey;
        private readonly string indexName;
        private readonly AzureKeyCredential creds;
        private readonly string vectorAlgorithmConfigName;
        private readonly string vectorProfileName;

        /// <summary>
        /// Initializes the ingestion service and creates Azure AI Search clients from the configured settings.
        /// </summary>
        /// <param name="config">Application configuration containing Azure AI Search connection and index settings.</param>
        /// <param name="embeddingService">Service used to create embeddings for document chunks.</param>
        /// <param name="pdfService">Service used to extract and chunk PDF content.</param>
        /// <param name="logger">Logger used to record ingestion progress.</param>
        public IngestionService(
            IConfiguration config,
            IEmbeddingService embeddingService,
            IPdfHelperService pdfService,
            ILogger<IngestionService> logger) 
        {
            _config = config;
            _embeddingService = embeddingService;
            _pdfService = pdfService;
            _logger = logger;

            searchServiceEndpoint = _config["AzureAiSearch:Endpoint"]! ;
            searchServiceApiKey = _config["AzureAiSearch:ApiKey"]!;
            indexName = _config["AzureAiSearch:IndexName"]!;
            vectorAlgorithmConfigName = _config["AzureAiSearch:VectorAlgorithmName"]!;
            vectorProfileName = _config["AzureAiSearch:VectorProfileName"]!;



            creds = new(searchServiceApiKey);
            searchClient = new SearchClient(new Uri(searchServiceEndpoint), indexName, creds);
            searchIndexClient = new(new Uri(searchServiceEndpoint), creds);
        }

        /// <summary>
        /// Extracts a document, splits its content into chunks, generates embeddings, and uploads the chunks to the search index.
        /// </summary>
        /// <param name="docName">Name of the uploaded document to ingest.</param>
        /// <returns>The number of text chunks uploaded.</returns>
        public async Task<int> IngestDocumentAsync(string docName)
        {
            _logger.LogInformation($"Ingesting document {docName}");

            await EnsureIndexExistsAsync();
            var content = await _pdfService.ExtractContentAsync(docName);
            _logger.LogInformation($"Content extraction complete:\n {content}");

            List<string> chunks = _pdfService.ChunkText(content);
            _logger.LogInformation($"Chunking of extracted content completed with chunk count: {chunks.Count}");

            _logger.LogInformation($"Sending all chunk for embedding");
            var doc = await _embeddingService.EmbedAsync(docName, chunks);

            _logger.LogInformation($"Embedding completed for document {docName}");
            _logger.LogInformation($"Uploading Document...");

            await searchClient.UploadDocumentsAsync(doc);
            _logger.LogInformation($"search Document upload completed");

            return chunks.Count;
        }

        /// <summary>
        /// Creates or updates the configured Azure AI Search index and its vector-search configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous index operation.</returns>
        private async Task EnsureIndexExistsAsync()
        {
            _logger.LogInformation($"Trying to create index if not exist");

            var fields = new SearchField[]
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true},
                new SimpleField("docName", SearchFieldDataType.String ) {IsFilterable = true},
                new SearchableField("content"),
                new VectorSearchField("contentVector", 1536,vectorProfileName)
            };

            var searchIndex = new SearchIndex(indexName, fields);
            searchIndex.VectorSearch = new VectorSearch();

            searchIndex.VectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration(vectorAlgorithmConfigName));
            searchIndex.VectorSearch.Profiles.Add(new VectorSearchProfile(vectorProfileName, vectorAlgorithmConfigName));

            _logger.LogInformation($"VectorProfile: {vectorProfileName}, VectorAlgo: {vectorAlgorithmConfigName}, IndexName: {indexName}");

            await searchIndexClient.CreateOrUpdateIndexAsync(searchIndex);
        }
    }
}
