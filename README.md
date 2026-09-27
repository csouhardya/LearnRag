# LearnRag solution guide

## Purpose and request flow

LearnRag is an ASP.NET Core .NET 10 API that accepts PDF uploads, extracts and chunks their text, creates Azure OpenAI embeddings, and stores the chunks in Azure AI Search. It can then embed a question, retrieve similar chunks, and ask an Azure OpenAI chat deployment to answer using the retrieved text.

The ingestion flow is `AiController.UploadAsync` → `IngestionService.IngestDocumentAsync` → `PdfHelperService.ExtractContentAsync` / `ChunkText` → `EmbeddingService.EmbedAsync(docName, chunks)` → Azure AI Search upload. The question flow is `AiController.GetAsync` → `AskService.AskAsync` → `EmbeddingService.EmbedAsync(question)` → Azure AI Search vector search → Azure OpenAI chat completion → HTTP response.

## Source files and responsibilities

- `Program.cs`: creates and configures the web application, registers dependencies, configures middleware, maps controllers, and starts the server.
- `Controllers/AiController.cs`: exposes HTTP endpoints for asking questions and uploading PDFs.
- `Services/IngestionService.cs`: creates/configures the search index and orchestrates document ingestion.
- `Services/PdfHelperService.cs`: reads PDF pages and splits extracted text into chunks.
- `Services/EmbeddingService.cs`: gets embeddings for document chunks or a question.
- `Services/AskService.cs`: retrieves relevant chunks and generates an answer from them.
- `Interfaces/IAskService.cs`, `IIngestionService.cs`, `IEmbeddingService.cs`, `IPdfHelperService.cs`: contracts used by controllers and services.
- `Models/AskRequest.cs`, `IngestRequest.cs`, `AskResponse.cs`: request/response data types.

## `Program.cs`

### Top-level statements, line by line

1. Imports Azure OpenAI, the LearnRag service interfaces and implementations, and the OpenAI namespace.
2. Creates a `WebApplicationBuilder`, which provides configuration, dependency injection, logging, and hosting.
3. Reads `AzureOpenAi:Endpoint` from configuration for the Azure OpenAI resource URL.
4. Reads `AzureOpenAi:ApiKey` from configuration for authentication.
5. Registers MVC controller support.
6. Registers OpenAPI document generation.
7. Registers one shared `AzureOpenAIClient` singleton. The endpoint is parsed as a URI and the key is wrapped in an API-key credential.
8. Registers `IIngestionService` with `IngestionService` as a scoped service.
9. Registers `IEmbeddingService` with `EmbeddingService` as a scoped service.
10. Registers `IPdfHelperService` with `PdfHelperService` as a scoped service.
11. Registers `IAskService` with `AskService` as a scoped service.
12. Builds the application and its dependency-injection container.
13. In Development only, maps the generated OpenAPI document at the framework's OpenAPI route.
14. Adds HTTPS redirection middleware.
15. Adds authorization middleware.
16. Maps controller routes to HTTP endpoints.
17. Starts the web application and begins processing requests.

## `Controllers/AiController.cs`

### `AiController` class

The controller is marked with `[ApiController]`, enabling ASP.NET Core API behavior such as request binding and validation responses. `[Route("[controller]")]` makes the base route `/Ai`, based on the controller name. Its primary constructor receives configuration, ingestion and question-answering service implementations, and a logger. The `uploadFolder` field reads `Constants:UploadFolderPath`; the other fields retain the injected service and logger instances.

### `GetAsync(AskRequest request)`

1. `[HttpGet]` and `[Route("ask")]` expose this method at `GET /Ai/ask`.
2. The method enters a `try` block so service failures can be returned as a client error response.
3. It passes `request.userPrompt` to `IAskService.AskAsync` and awaits the answer.
4. It returns the answer object with HTTP 200 (`Ok`).
5. If an exception occurs, it logs the inner exception text when present.
6. It returns the exception message with HTTP 400 (`BadRequest`).

### `UploadAsync(IFormFile file)`

1. `[HttpPost]` and `[Route("upload")]` expose this method at `POST /Ai/upload`.
2. The first `try` block handles saving the uploaded file.
3. If the configured upload directory does not exist, it creates it.
4. It combines the upload directory and uploaded file name to form the destination path.
5. It opens that path for writing, replacing a file at the same path if one exists.
6. It asynchronously copies the request file contents into the destination stream. The `using` statement disposes the stream when copying completes.
7. If saving fails, it logs available inner-exception text and returns HTTP 400 with the exception message.
8. A second `try` block starts document ingestion after the upload is saved.
9. It calls `IngestDocumentAsync` with the uploaded file name and awaits the number of chunks ingested.
10. It returns that result with HTTP 200.
11. If ingestion fails, it logs available inner-exception text and returns HTTP 400 with the exception message.

## `Services/IngestionService.cs`

### `IngestionService` class and constructor

The service implements `IIngestionService`. It keeps Azure AI Search document and index clients, configuration, embedding/PDF services, a logger, and configured endpoint, key, index, algorithm, and profile values.

The constructor:
1. Receives configuration, embedding service, PDF helper service, and logger through dependency injection.
2. Stores these dependencies for later use.
3. Reads the Azure AI Search endpoint, API key, index name, vector algorithm name, and vector profile name from configuration.
4. Creates an `AzureKeyCredential` using the configured key.
5. Creates a `SearchClient` for document operations against the configured index.
6. Creates a `SearchIndexClient` for index operations against the search service.

### `IngestDocumentAsync(string docName)`

1. Logs that ingestion has started for the document.
2. Awaits `EnsureIndexExistsAsync` so the target index is ready before documents are uploaded.
3. Awaits PDF text extraction for the provided file name.
4. Logs the extracted text.
5. Splits the extracted text into chunks using `IPdfHelperService.ChunkText`.
6. Logs the number of chunks.
7. Logs that embedding generation is beginning.
8. Awaits `IEmbeddingService.EmbedAsync(docName, chunks)` to create search documents with their vectors.
9. Logs that embedding has completed and that upload is beginning.
10. Awaits `SearchClient.UploadDocumentsAsync` to upload all resulting search documents.
11. Logs upload completion.
12. Returns the number of text chunks.

### `EnsureIndexExistsAsync()`

1. Logs that index creation/update is starting.
2. Defines the index schema: `id` is a string key, `docName` is a filterable string, `content` is searchable text, and `contentVector` is a vector field with 1536 dimensions using the configured vector profile.
3. Creates a `SearchIndex` from the configured index name and schema fields.
4. Assigns a new vector-search configuration to the index.
5. Adds the configured HNSW vector algorithm.
6. Adds a vector profile that associates the configured profile name with that algorithm.
7. Logs the profile, algorithm, and index names.
8. Awaits `CreateOrUpdateIndexAsync` to create the index or update its definition in Azure AI Search.

## `Services/PdfHelperService.cs`

### `PdfHelperService` class and primary constructor

The service implements `IPdfHelperService`. Its primary constructor receives application configuration and a logger. It reads `Constants:UploadFolderPath` into `uploadFolder` and stores the logger.

### `ChunkText(string content)`

1. Defines the separator as two CRLF sequences (`\r\n\r\n`).
2. Creates an empty list of chunks.
3. Splits the provided content at each exact separator occurrence.
4. Adds each resulting paragraph to the list.
5. Returns the list. The current implementation does not trim or filter empty chunks.

### `ExtractContentAsync(string docName)`

1. Creates a `StringBuilder` to accumulate extracted page text.
2. Initializes a page counter to zero.
3. Combines the configured upload folder and document name into a file path.
4. Logs the resolved path and that extraction is starting.
5. Opens the PDF with PdfPig inside a `using` scope so the PDF is disposed after extraction.
6. Iterates through the PDF's pages.
7. For each page, logs its page number and the document's total page count.
8. Increments the local page counter.
9. Extracts the page text in content/layout order and appends it, requesting double-newline separators.
10. Appends a visible page separator containing the local page number.
11. After all pages are processed, returns the accumulated text as a string.

## `Services/EmbeddingService.cs`

### `EmbeddingService` class and primary constructor

The service implements `IEmbeddingService`. Its primary constructor receives and stores an `AzureOpenAIClient`, configuration, and logger. It reads `AzureAiSearch:DeploymentName` for the embedding deployment.

### `EmbedAsync(string docName, List<string> chunks)`

1. Removes the portion of `docName` beginning at the first period; this value is used as the document identifier prefix.
2. Logs the document embedding operation.
3. Gets an embedding client for the configured deployment.
4. Logs that the embedding client is available.
5. Creates a list for the resulting Azure AI Search documents.
6. Iterates through chunk numbers from 1 through the number of chunks.
7. Logs the current chunk number and total.
8. Requests an embedding for the current chunk and awaits the result.
9. Converts the embedding to a `float[]`.
10. Logs completion for that chunk.
11. Creates a `SearchDocument` with an ID in the form `<docName>-chunk-<number>`, the normalized document name, source text, and vector.
12. Logs the vector values.
13. After all chunks are processed, returns the search-document list.

### `EmbedAsync(string query)`

1. Logs the query being embedded.
2. Gets an embedding client for the configured deployment.
3. Logs that the embedding client is available.
4. Requests and awaits an embedding for the query.
5. Logs embedding completion.
6. Converts the embedding to a `float[]`.
7. Logs the vector values.
8. Returns the vector for use in vector search.

## `Services/AskService.cs`

### `AskService` class and constructor

The service implements `IAskService`. The constructor receives the Azure OpenAI client, configuration, embedding service, and logger. It stores those dependencies, reads the search endpoint, search key, search index name, and chat model/deployment name from configuration, creates an Azure Search credential, and constructs a `SearchClient` for the selected index.

### `AskAsync(string question)`

1. Creates an empty list for retrieved text chunks.
2. Logs that the question is being sent for embedding.
3. Calls and awaits the embedding service for the question.
4. Logs that the question was converted to a vector.
5. Creates a `VectorizedQuery` from the vector, requests the two nearest neighbors, and selects the `contentVector` field.
6. Creates search options with a maximum result size of two.
7. Creates vector-search options and adds the vector query.
8. Logs that vector search is beginning.
9. Calls and awaits `SearchAsync<SearchDocument>` with those options.
10. Logs the result count reported by Azure AI Search.
11. Asynchronously iterates over search results.
12. For each result, checks whether its document contains a `content` field.
13. If present, converts the content to a string (or an empty string for null) and adds it to the chunk list.
14. Joins retrieved chunks with blank lines to form the context for the chat model.
15. Builds the system prompt. It instructs the model to answer only from the supplied policy context, return exactly `Unable to get the information` when the text is insufficient or ambiguous, and format applicable conditions clearly.
16. Logs the configured chat deployment and obtains its chat client.
17. Logs that chat completion is being called with the system context and user question.
18. Awaits `CompleteChatAsync` with a system message containing the policy prompt and a user message containing the question.
19. Builds an `AskResponse` from the first returned text content, model name, token usage, role, and tool calls.
20. Returns the response.

## Interfaces

These interfaces define the methods consumers can call and allow implementations to be replaced or mocked.

- `IAskService.AskAsync(string question)`: asynchronously returns an `AskResponse` for a question.
- `IIngestionService.IngestDocumentAsync(string docName)`: asynchronously ingests a named document and returns its chunk count.
- `IEmbeddingService.EmbedAsync(string docName, List<string> chunks)`: asynchronously returns search documents containing chunk text and embeddings.
- `IEmbeddingService.EmbedAsync(string query)`: asynchronously returns the embedding vector for a query.
- `IPdfHelperService.ChunkText(string content)`: synchronously splits text into chunks.
- `IPdfHelperService.ExtractContentAsync(string docName)`: asynchronously returns extracted PDF text.

## Models

### `AskRequest`

This request model has one property, `userPrompt`, which contains the question sent to the ask endpoint. `[Required]` tells ASP.NET Core model validation that a value is required.

### `IngestRequest`

This model has `DocumentTitle` and `Content` string properties for a document title and content. The current upload endpoint accepts an `IFormFile` directly and does not use this model.

### `AskResponse`

This response model contains:
- `Response`: generated answer text.
- `Model`: model/deployment identifier returned by the chat completion.
- `Usage`: optional chat token usage metadata.
- `Role`: optional chat message role.
- `Tools`: optional list of chat tool calls.

## Configuration keys used by the source

- `AzureOpenAi:Endpoint` and `AzureOpenAi:ApiKey`: Azure OpenAI client connection settings.
- `AzureOpenAi:Model`: chat deployment/model used by `AskService`.
- `AzureAiSearch:Endpoint`, `ApiKey`, and `IndexName`: Azure AI Search connection and index settings.
- `AzureAiSearch:DeploymentName`: embedding deployment used by `EmbeddingService`.
- `AzureAiSearch:VectorAlgorithmName` and `VectorProfileName`: vector index configuration.
- `Constants:UploadFolderPath`: local folder used to save and read uploaded PDFs.

Keep API keys in user secrets or an appropriate secret store; do not commit real credentials.

## Notes about current behavior

- Uploads are saved using the provided file name and then ingested from the configured local directory.
- Text chunking uses the exact `\r\n\r\n` delimiter.
- Each chunk embedding call is awaited individually.
- Vector search and index schema both use a 1536-dimensional vector field.
- The ask service currently requests up to two matching chunks and grounds the chat prompt on those chunks.
