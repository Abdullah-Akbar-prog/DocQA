using DocQA.Data;
using DocQA.Ingestion;
using DocQA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocQA.Features.Documents
{
    public sealed record DocumentDto(
        Guid Id, string FileName, DocumentStatus Status, int ChunkCount, string? Error, DateTimeOffset CreatedAt);

    public static class DocumentEndpoints
    {
        private static readonly string[] AllowedExtensions = [".pdf", ".txt", ".md"];

        public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/documents").WithTags("Documents");

            group.MapPost("/", Upload).DisableAntiforgery();
            group.MapGet("/", List);
            group.MapGet("/{id:guid}", Get);
            group.MapDelete("/{id:guid}", Delete);

            return app;
        }

        private static async Task<IResult> Upload(
            IFormFile file,
            AppDbContext db,
            IWebHostEnvironment env,
IngestionQueue queue,
            IOptions<StorageOptions> storage,
            CancellationToken ct)
        {
            var settings = storage.Value;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                return Invalid($"Unsupported file type. Allowed: {string.Join(", ", AllowedExtensions)}");

            if (file.Length == 0 || file.Length > settings.MaxUploadBytes)
                return Invalid($"File must be between 1 byte and {settings.MaxUploadBytes / (1024 * 1024)} MB.");

            if (extension == ".pdf" && !await LooksLikePdfAsync(file, ct))
                return Invalid("This file is not a valid PDF.");

            var uploadDir = Path.Combine(env.ContentRootPath, settings.UploadPath);
            Directory.CreateDirectory(uploadDir);

            // The stored name is our own GUID. The client's file name is only kept for display.
            var id = Guid.NewGuid();
            var storagePath = Path.Combine(uploadDir, $"{id}{extension}");

            await using (var stream = File.Create(storagePath))
            {
                await file.CopyToAsync(stream, ct);
            }

            var doc = new Document { Id = id, FileName = DisplayName(file.FileName), StoragePath = storagePath };
            db.Documents.Add(doc);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                File.Delete(storagePath); // don't leave an orphan file if the database write fails
                throw;
            }
            await queue.EnqueueAsync(id, ct);
            return Results.Accepted($"/api/documents/{id}", ToDto(doc));
        }

        private static async Task<IResult> List(AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Documents
                .AsNoTracking()
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DocumentDto(d.Id, d.FileName, d.Status, d.ChunkCount, d.Error, d.CreatedAt))
                .ToListAsync(ct));

        private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct)
        {
            var doc = await db.Documents
                .AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new DocumentDto(d.Id, d.FileName, d.Status, d.ChunkCount, d.Error, d.CreatedAt))
                .FirstOrDefaultAsync(ct);

            return doc is null ? Results.NotFound() : Results.Ok(doc);
        }

        private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound();

            db.Documents.Remove(doc); // chunks are removed by the cascade from step 2
            await db.SaveChangesAsync(ct);

            if (File.Exists(doc.StoragePath)) File.Delete(doc.StoragePath);
            return Results.NoContent();
        }

        private static IResult Invalid(string message) =>
            Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

        private static string DisplayName(string clientName)
        {
            var name = Path.GetFileName(clientName);
            return name.Length > 260 ? name[..260] : name;
        }

        // Checks the file's first bytes instead of trusting the extension.
        private static async Task<bool> LooksLikePdfAsync(IFormFile file, CancellationToken ct)
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[5];
            var read = await stream.ReadAsync(header, ct);
            return read == 5 && header.AsSpan().SequenceEqual("%PDF-"u8);
        }

        private static DocumentDto ToDto(Document d) =>
            new(d.Id, d.FileName, d.Status, d.ChunkCount, d.Error, d.CreatedAt);
    }
}
