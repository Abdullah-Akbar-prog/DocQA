using DocQA.Data;

namespace DocQA.Models;

public enum DocumentStatus { Pending, Processing, Ready, Failed }

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public required string StoragePath { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? Error { get; set; }
    public int ChunkCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public List<Chunk> Chunks { get; set; } = [];
}