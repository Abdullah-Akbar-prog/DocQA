using DocQA;
using DocQA.Data;
using DocQA.Features.Documents;
using DocQA.Features.Search;
using DocQA.Ingestion;
using DocQA.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Text.Json.Serialization;
using static DocQA.Models.Options;

var builder = WebApplication.CreateBuilder(args);

// Controllers and API documentation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Application options
builder.Services.AddOptions<StorageOptions>()
    .BindConfiguration("Storage");

builder.Services.AddOptions<OllamaOptions>()
    .BindConfiguration("Ollama");

builder.Services.AddOptions<RagOptions>()
    .BindConfiguration("Rag");

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()));

// CORS
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(
                builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));

// Ollama client factory
static OllamaApiClient CreateOllama(
    IServiceProvider sp,
    string model)
{
    var options = sp
        .GetRequiredService<IOptions<OllamaOptions>>()
        .Value;

    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(options.Endpoint),
        Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
    };

    return new OllamaApiClient(httpClient, model);
}

// Chat model
builder.Services.AddSingleton<IChatClient>(sp =>
    CreateOllama(
        sp,
        sp.GetRequiredService<IOptions<OllamaOptions>>()
            .Value.ChatModel));

// Embedding model
builder.Services
    .AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        CreateOllama(
            sp,
            sp.GetRequiredService<IOptions<OllamaOptions>>()
                .Value.EmbeddingModel));

// Document ingestion services
builder.Services.AddSingleton<ITextExtractor, TextExtractor>();
builder.Services.AddSingleton<IngestionQueue>();
builder.Services.AddHostedService<IngestionWorker>();

// RAG and search services
builder.Services.AddSingleton<VectorIndex>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddScoped<AskService>();

var app = builder.Build();

// HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

// API endpoints
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapControllers();

app.Run();