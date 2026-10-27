using DocQA.Data;
using DocQA.Models;
using DocQA.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using static DocQA.Models.Options;

namespace DocQA.Ingestion
{
    public sealed class IngestionWorker(
        IServiceScopeFactory scopes,
        IngestionQueue queue,
        ITextExtractor extractor,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        IOptions<RagOptions> options,
        ILogger<IngestionWorker> logger) : BackgroundService
    {
        private const int EmbeddingBatchSize = 16;
        private const int MaxAttempts = 3;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeueUnfinishedAsync(stoppingToken);

            try
            {
                await foreach (var id in queue.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessWithRetryAsync(id, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(ex, "Unexpected ingestion error for document {DocumentId}", id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown. Unfinished documents are picked up again on the next start.
            }
        }

        /// <summary>After a restart, anything left Pending or Processing goes back on the queue.</summary>
        private async Task RequeueUnfinishedAsync(CancellationToken ct)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await db.Documents
                    .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Processing)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                foreach (var id in ids) await queue.EnqueueAsync(id, ct);
                if (ids.Count > 0) logger.LogInformation("Re-queued {Count} unfinished document(s)", ids.Count);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not re-queue unfinished documents");
            }
        }

        private async Task ProcessWithRetryAsync(Guid id, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await IngestAsync(id, ct);
                    return;
                }
                catch (IngestionException ex) // permanent problem: don't retry
                {
                    await MarkFailedAsync(id, ex.Message, ct);
                    return;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested) // maybe temporary: retry
                {
                    if (attempt >= MaxAttempts)
                    {
                        logger.LogError(ex, "Ingestion of {DocumentId} failed after {Attempts} attempts", id, attempt);
                        await MarkFailedAsync(id, $"Failed after {MaxAttempts} attempts: {ex.Message}", ct);
                        return;
                    }

                    logger.LogWarning(ex, "Ingestion attempt {Attempt} failed for {DocumentId}; retrying", attempt, id);
                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
                }
            }
        }
        private async Task IngestAsync(Guid id, CancellationToken ct)
        {
            var rag = options.Value;

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null || doc.Status == DocumentStatus.Ready) return;

            doc.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync(ct);

            // A previous attempt may have left partial chunks behind.
            await db.Chunks.Where(c => c.DocumentId == id).ExecuteDeleteAsync(ct);

            var text = await extractor.ExtractAsync(doc.StoragePath, Path.GetExtension(doc.StoragePath), ct);
            var pieces = TextChunker.Chunk(text, rag.ChunkSize, rag.ChunkOverlap);
            if (pieces.Count == 0)
                throw new IngestionException("No extractable text found (is this a scanned PDF?).");

            var chunks = new List<Chunk>(pieces.Count);
            for (var i = 0; i < pieces.Count; i += EmbeddingBatchSize)
            {
                var batch = pieces.Skip(i).Take(EmbeddingBatchSize).ToList();
                var inputs = batch.Select(p => rag.DocumentPrefix + p).ToList();
                var embeddings = await embedder.GenerateAsync(inputs, cancellationToken: ct);

                for (var j = 0; j < batch.Count; j++)
                {
                    var vector = embeddings[j].Vector;
                    if (vector.Length != AppDbContext.EmbeddingDimensions)
                        throw new IngestionException(
                            $"Embedding model returned {vector.Length} dimensions but the database expects {AppDbContext.EmbeddingDimensions}.");

                    chunks.Add(new Chunk
                    {
                        DocumentId = id,
                        Index = i + j,
                        Content = batch[j],
                        Embedding = VectorMath.ToBytes(vector)
                    });
                }
            }

            db.Chunks.AddRange(chunks);
            doc.ChunkCount = chunks.Count;
            doc.Status = DocumentStatus.Ready;
            doc.Error = null;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Ingested {FileName} into {Chunks} chunks", doc.FileName, chunks.Count);
        }
        private async Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return;

            doc.Status = DocumentStatus.Failed;
            doc.Error = error.Length > 500 ? error[..500] : error;
            await db.SaveChangesAsync(ct);

            logger.LogWarning("Document {FileName} failed: {Error}", doc.FileName, error);
        }
    }
}
