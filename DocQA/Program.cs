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

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<AppDbContext>
(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOptions<StorageOptions>().BindConfiguration("Storage");
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddOptions<OllamaOptions>().BindConfiguration("Ollama");
builder.Services.AddOptions<RagOptions>().BindConfiguration("Rag");

// Only this registration knows about Ollama. Everything else uses the IEmbeddingGenerator interface.
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var ollama = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var http = new HttpClient
    {
        BaseAddress = new Uri(ollama.Endpoint),
        Timeout = TimeSpan.FromSeconds(ollama.TimeoutSeconds)
    };
    return new OllamaApiClient(http, ollama.EmbeddingModel);
});

builder.Services.AddSingleton<ITextExtractor, TextExtractor>();
builder.Services.AddSingleton<IngestionQueue>();
builder.Services.AddHostedService<IngestionWorker>();
builder.Services.AddSingleton<VectorIndex>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddScoped<AskService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapDocumentEndpoints();
app.MapSearchEndpoints();

app.MapControllers();

app.Run();
