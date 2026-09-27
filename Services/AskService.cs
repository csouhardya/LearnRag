using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using LearnRag.Interfaces;
using LearnRag.Models;
using OpenAI.Chat;

namespace LearnRag.Services
{
    public class AskService: IAskService
    {
        private readonly AzureOpenAIClient _azOpenAiClient; // to communicate with azure openai
        private readonly IConfiguration _config;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<AskService> _logger;

        private readonly AzureKeyCredential creds;
        private readonly SearchClient searchClient; // to communicate with azure ai search, uploading document, search for relevant chunk

        private readonly string azOpenAiDeployment;
        private readonly string searchServiceEndpoint;
        private readonly string searchServiceApiKey;
        private readonly string indexName;

        public AskService(
            AzureOpenAIClient azOpenAiClient,
            IConfiguration config,
            IEmbeddingService embeddingService,
            ILogger<AskService> logger) 
        {
            _config = config;
            _azOpenAiClient = azOpenAiClient;
            _embeddingService = embeddingService;
            _logger = logger;

            searchServiceEndpoint = _config["AzureAiSearch:Endpoint"]! ;
            searchServiceApiKey = _config["AzureAiSearch:ApiKey"]!;
            indexName = _config["AzureAiSearch:IndexName"]!;
            azOpenAiDeployment = _config["AzureOpenAi:Model"]!;

            creds = new(searchServiceApiKey);
            searchClient = new SearchClient(new Uri(searchServiceEndpoint), indexName, creds);
        }

        public async Task<AskResponse> AskAsync(string question)
        {
            List<string> chunks = [];

            _logger.LogInformation($"{question} sent to embed");
            var vector = await _embeddingService.EmbedAsync(question); // Embed the user query to compare

            _logger.LogInformation($"userPrompt to float array successfull");
            var vQuery = new VectorizedQuery(vector)
            {
                KNearestNeighborsCount = 2, // top 2 nearest cluster
                Fields = {"contentVector"} // vector column in azure index
            };

            var options = new SearchOptions { Size = 2 };
            options.VectorSearch = new VectorSearchOptions();
            options.VectorSearch.Queries.Add(vQuery);

            _logger.LogInformation($"Sending vector to searchAsync with options: {options.Size}");
            var results = await searchClient.SearchAsync<SearchDocument>(options); // will return us the context in chunks
            _logger.LogInformation($"searchAsync result returned successfully with context chunk size: {results.Value.TotalCount}");

            await foreach (var r in results.Value.GetResultsAsync())
            {
                if(r.Document.TryGetValue("content", out var content))
                {
                    chunks.Add(content?.ToString() ?? string.Empty);
                }
            }

            var context = string.Join("\n\n", chunks);

            // Now to pass the userQuery vector along with context to LLM
            string systemprompt = $"""
                    You are an expert HR Assistant. Your only task is to answer user questions accurately using the provided policy text segments. 

                Strictly adhere to the following operational boundaries:
                1. Grounding: Answer the question using ONLY the explicit facts mentioned in the [POLICY CONTEXT] block below. Do not assume, extrapolate, or bring in any outside corporate rules, legal frameworks, or general knowledge.
                2. Handling Gaps: If the answer cannot be completely derived from the text provided below, or if the text is ambiguous regarding the query, you must respond with exactly: "Unable to get the information". Do not provide partial explanations or speculative guesses.
                3. Formatting: Structure your response cleanly. If the context contains specific conditions (e.g., eligibility requirements, timelines), list them clearly.

                [POLICY CONTEXT]
                    {context}
                """;

            /* If using a smaller model phi-4-mini-instruct is a smaller model, then there's a tendency to suffer from "Prompt Bleeding" as 
             * they easily forget system instructions when their internal pre-trained world knowledge overrides the prompt constraints. To minimize,
             * put your guard rails in one single prompt instead of system and user prompt */
            //string reinforcedUserPrompt =
            //   $"CRITICAL RULE: Answer the question below using ONLY the facts in the [POLICY CONTEXT]. " +
            //   $"If the answer is not explicitly written there, reply exactly with: \"Unable to get the information\". " +
            //   $"Do not use any outside knowledge.\n\n" +
            //   $"[POLICY CONTEXT]\n{context}\n\n" +
            //   $"[USER QUESTION]\n{question}";

            _logger.LogInformation($"Connecting with azure chat client, deploymentName: {azOpenAiDeployment}");
            var chatClient = _azOpenAiClient.GetChatClient(azOpenAiDeployment);

            _logger.LogInformation($"Calling complete chat async with systemprompt:\n {systemprompt} and userprompt:\n {question}");
            var result = await chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemprompt),
                    new UserChatMessage(question)
                 ]);

            AskResponse resp = new()
            {
                Response = result.Value.Content[0].Text,
                Model = result.Value.Model,
                Usage = result.Value.Usage,
                Role = result.Value.Role,
                Tools = result.Value.ToolCalls,
            };
            return resp;
        }
    }
}
