using DocQA.Models;

namespace DocQA.Dto
{
    public sealed record DocumentDto(
    Guid Id, string FileName, DocumentStatus Status, int ChunkCount, string? Error, DateTimeOffset CreatedAt);
}
