using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using static DocQA.Models.Options;

namespace DocQA.Rag;

public sealed record SourceDto(int Id, Guid DocumentId, string FileName, int ChunkIndex, double Distance, string Snippet);

public sealed record AskResult(string Answer, IReadOnlyList<SourceDto> Sources, long? InputTokens, long? OutputTokens);

public abstract record StreamEvent;
public sealed record SourcesEvent(IReadOnlyList<SourceDto> Sources) : StreamEvent;
public sealed record TokenEvent(string Text) : StreamEvent;
public sealed record DoneEvent(bool Declined) : StreamEvent;

public sealed class AskService(RetrievalService retrieval, IChatClient chat, IOptions<RagOptions> options)
{
    private sealed record Prepared(List<RetrievedChunk> Relevant, List<ChatMessage>? Messages);

    public async Task<AskResult> AskAsync(string question, int? topK, string userId, CancellationToken ct)
    {
        var prepared = await PrepareAsync(question, userId, topK, ct);
        if (prepared.Messages is null)
            return new AskResult(PromptBuilder.NoAnswer, [], null, null);

        var response = await chat.GetResponseAsync(prepared.Messages, GenerationOptions(), ct);
        var answer = response.Text.Trim();

        return new AskResult(
            answer,
            IsDeclined(answer) ? [] : ToSources(prepared.Relevant),
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
    }

    /// <summary>Sources first, then the answer token by token, then a final "done" event.</summary>
    public async IAsyncEnumerable<StreamEvent> AskStreamAsync(
        string question, int? topK, string userId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prepared = await PrepareAsync(question, userId, topK, ct);

        if (prepared.Messages is null)
        {
            yield return new SourcesEvent([]);
            yield return new TokenEvent(PromptBuilder.NoAnswer);
            yield return new DoneEvent(Declined: true);
            yield break;
        }

        yield return new SourcesEvent(ToSources(prepared.Relevant));

        var answer = new StringBuilder();
        await foreach (var update in chat.GetStreamingResponseAsync(prepared.Messages, GenerationOptions(), ct))
        {
            if (string.IsNullOrEmpty(update.Text)) continue;
            answer.Append(update.Text);
            yield return new TokenEvent(update.Text);
        }

        yield return new DoneEvent(IsDeclined(answer.ToString().Trim()));
    }

    private async Task<Prepared> PrepareAsync(string question, string userId, int? topK, CancellationToken ct)
    {
        var found = await retrieval.SearchAsync(question, userId, topK, ct);
        var relevant = found.Where(c => c.Distance <= options.Value.MaxDistance).ToList();

        // Honest failure: nothing close enough means we don't ask the model to guess.
        return relevant.Count == 0
            ? new Prepared(relevant, null)
            : new Prepared(relevant, PromptBuilder.Build(question, relevant));
    }

    private static ChatOptions GenerationOptions() => new() { Temperature = 0.1f };

    private static bool IsDeclined(string answer) =>
        answer.StartsWith(PromptBuilder.NoAnswer, StringComparison.OrdinalIgnoreCase);

    private static List<SourceDto> ToSources(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks
            .Select((c, i) => new SourceDto(
                i + 1, c.DocumentId, c.FileName, c.ChunkIndex, Math.Round(c.Distance, 4), Snippet(c.Content)))
            .ToList();

    private static string Snippet(string content) =>
        content.Length <= 200 ? content : content[..200] + "…";
}