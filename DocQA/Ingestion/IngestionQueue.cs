using System.Threading.Channels;

namespace DocQA.Ingestion
{
    /// <summary>In-process queue of document ids waiting to be processed.</summary>
    public sealed class IngestionQueue
    {
        private readonly Channel<Guid> _channel =
               Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    }
}
