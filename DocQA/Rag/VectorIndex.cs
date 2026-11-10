using DocQA.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DocQA.Rag;

public sealed record VectorEntry(Guid ChunkId, Guid DocumentId, string FileName, int ChunkIndex, float[] Vector);

public sealed record VectorHit(VectorEntry Entry, double Distance);

public sealed class VectorIndex(IServiceScopeFactory scopes, ILogger<VectorIndex> logger)
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile VectorEntry[]? _entries;
    private int _version;

    /// <summary>Call after chunks are added or removed, so the next search reloads.</summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _version);
        _entries = null;
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(ReadOnlyMemory<float> query, int k, CancellationToken ct)
    {
        var entries = _entries ?? await LoadAsync(ct);
        return Rank(entries, VectorMath.Normalize(query.ToArray()), k);
    }

    /// <summary>The ranking logic on its own, so it can be unit tested without a database.</summary>
    public static IReadOnlyList<VectorHit> Rank(IEnumerable<VectorEntry> entries, float[] normalizedQuery, int k) =>
        entries
            .Where(e => e.Vector.Length == normalizedQuery.Length)
            .Select(e => new VectorHit(e, Math.Clamp(1.0 - VectorMath.Dot(e.Vector, normalizedQuery), 0.0, 2.0)))
            .OrderBy(h => h.Distance)
            .Take(k)
            .ToList();

    private async Task<VectorEntry[]> LoadAsync(CancellationToken ct)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            if (_entries is { } existing) return existing;

            var stopwatch = Stopwatch.StartNew();
            var versionAtStart = Volatile.Read(ref _version);

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var rows = await db.Chunks
                .AsNoTracking()
                .Select(c => new { c.Id, c.DocumentId, c.Document.FileName, c.Index, c.Embedding })
                .ToListAsync(ct);

            var loaded = rows
                .Select(r => new VectorEntry(
                    r.Id, r.DocumentId, r.FileName, r.Index,
                    VectorMath.Normalize(VectorMath.ToFloats(r.Embedding))))
                .ToArray();

            // Only cache the result if nothing changed while we were loading.
            if (versionAtStart == Volatile.Read(ref _version)) _entries = loaded;

            logger.LogInformation("Vector index loaded: {Count} chunks in {Ms} ms", loaded.Length, stopwatch.ElapsedMilliseconds);
            return loaded;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}