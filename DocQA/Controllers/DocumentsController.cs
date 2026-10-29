using DocQA.Data;
using DocQA.Dto;
using DocQA.Ingestion;
using DocQA.Models;
using DocQA.Rag;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocQA.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(
    AppDbContext db,
    IngestionQueue queue,
    VectorIndex index,
    ITextExtractor extractor,
    IWebHostEnvironment env,
    IOptions<StorageOptions> storage) : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".pdf", ".txt", ".md"];

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
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

        await using (var stream = System.IO.File.Create(storagePath))
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
            System.IO.File.Delete(storagePath); // don't leave an orphan file if the database write fails
            throw;
        }

        await queue.EnqueueAsync(id, ct);
        return AcceptedAtAction(nameof(Get), new { id }, ToDto(doc));
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> List(CancellationToken ct) =>
        await db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentDto(d.Id, d.FileName, d.Status, d.ChunkCount, d.Error, d.CreatedAt))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DocumentDto(d.Id, d.FileName, d.Status, d.ChunkCount, d.Error, d.CreatedAt))
            .FirstOrDefaultAsync(ct);

        if (doc is null) return NotFound();
        return doc;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();

        db.Documents.Remove(doc); // chunks are removed by the cascade
        await db.SaveChangesAsync(ct);
        index.Invalidate();

        if (System.IO.File.Exists(doc.StoragePath)) System.IO.File.Delete(doc.StoragePath);
        return NoContent();
    }

    // Temporary helper from step 6. Delete it if you didn't add it or no longer need it.
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();

        var text = await extractor.ExtractAsync(doc.StoragePath, Path.GetExtension(doc.StoragePath), ct);
        var chunks = TextChunker.Chunk(text);

        return Ok(new { characters = text.Length, chunkCount = chunks.Count, firstChunks = chunks.Take(3) });
    }

    private IActionResult Invalid(string message)
    {
        ModelState.AddModelError("file", message);
        return ValidationProblem(ModelState);
    }

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