using DocQA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using static DocQA.Models.Options;

namespace DocQA.Rag;

public sealed record RetrievedChunk(Guid DocumentId, string FileName, int ChunkIndex, string Content, double Distance);

public sealed class RetrievalService(
    AppDbContext db,
    VectorIndex index,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IOptions<RagOptions> options)
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string question, int? topK, CancellationToken ct)
    {
        var rag = options.Value;
        var k = Math.Clamp(topK ?? rag.TopK, 1, 20);

        var embeddings = await embedder.GenerateAsync(new[] { rag.QueryPrefix + question }, cancellationToken: ct);

        // 1. Find the closest chunks in memory (ids and distances only).
        var hits = await index.SearchAsync(embeddings[0].Vector, k, ct);
        if (hits.Count == 0) return [];

        // 2. Load the text of just those chunks from SQL Server.
        var ids = hits.Select(h => h.Entry.ChunkId).ToList();
        var contents = await db.Chunks
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Content })
            .ToDictionaryAsync(c => c.Id, c => c.Content, ct);

        // A chunk can vanish between the two steps if its document was just deleted. Skip those.
        return hits
            .Where(h => contents.ContainsKey(h.Entry.ChunkId))
            .Select(h => new RetrievedChunk(
                h.Entry.DocumentId, h.Entry.FileName, h.Entry.ChunkIndex, contents[h.Entry.ChunkId], h.Distance))
            .ToList();
    }
}