using DocQA;
using DocQA.Data;
using DocQA.Ingestion;
using DocQA.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Text.Json.Serialization;
using static DocQA.Models.Options;

var builder = WebApplication.CreateBuilder(args);

// CHANGED: the enum-as-string setting moves here, because controllers have their own JSON options.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>
(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOptions<StorageOptions>().BindConfiguration("Storage");

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddOptions<OllamaOptions>().BindConfiguration("Ollama");
builder.Services.AddOptions<RagOptions>().BindConfiguration("Rag");

static OllamaApiClient CreateOllama(IServiceProvider sp, string model)
{
    var ollama = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var http = new HttpClient
    {
        BaseAddress = new Uri(ollama.Endpoint),
        Timeout = TimeSpan.FromSeconds(ollama.TimeoutSeconds)
    };
    return new OllamaApiClient(http, model);
}

// Only these two registrations know about Ollama. The rest of the code uses the interfaces.
builder.Services.AddSingleton<IChatClient>(sp =>
    CreateOllama(sp, sp.GetRequiredService<IOptions<OllamaOptions>>().Value.ChatModel));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    CreateOllama(sp, sp.GetRequiredService<IOptions<OllamaOptions>>().Value.EmbeddingModel));

builder.Services.AddSingleton<ITextExtractor, TextExtractor>();
builder.Services.AddSingleton<IngestionQueue>();
builder.Services.AddHostedService<IngestionWorker>();
builder.Services.AddSingleton<VectorIndex>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddScoped<AskService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

// CHANGED: the three app.Map...Endpoints() lines are gone. Controllers are found automatically.
app.MapControllers();

app.Run();