using System.Threading.Channels;

namespace DocQA.Ingestion
{
    /// <summary>In-process queue of document ids waiting to be processed.</summary>
    public sealed class IngestionQueue
    {
        private readonly Channel<Guid> _channel =
               Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

        public ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default) =>
             _channel.Writer.WriteAsync(documentId, ct);

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
                _channel.Reader.ReadAllAsync(ct);
    }
}
