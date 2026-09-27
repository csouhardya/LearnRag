using Azure.AI.OpenAI;
using LearnRag.Interfaces;
using LearnRag.Services;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string azOpenAiEndpoint = builder.Configuration["AzureOpenAi:Endpoint"]!;
string azOpenAiApiKey = builder.Configuration["AzureOpenAi:ApiKey"]!;
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton(sp => new AzureOpenAIClient(new Uri(azOpenAiEndpoint), new System.ClientModel.ApiKeyCredential(azOpenAiApiKey)));
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IPdfHelperService, PdfHelperService>();
builder.Services.AddScoped<IAskService, AskService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
