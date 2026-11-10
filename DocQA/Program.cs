using DocQA;
using DocQA.Auth;
using DocQA.Data;
using DocQA.Ingestion;
using DocQA.Rag;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OllamaSharp;
using System.Text;
using System.Text.Json.Serialization;
using static DocQA.Models.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Adds an "Authorize" button to Swagger UI.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>
(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOptions<StorageOptions>().BindConfiguration("Storage");

// ---- Authentication ----
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 8;          // plus upper, lower and a digit (defaults)
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt");
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random value>\" --project src/DocQA.Api");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names as written in the token ("sub", "email")
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<TokenService>();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();