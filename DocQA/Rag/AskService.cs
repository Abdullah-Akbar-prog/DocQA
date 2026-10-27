using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using static DocQA.Models.Options;

namespace DocQA.Rag;

public sealed record SourceDto(int Id, Guid DocumentId, string FileName, int ChunkIndex, double Distance, string Snippet);

public sealed record AskResult(string Answer, IReadOnlyList<SourceDto> Sources, long? InputTokens, long? OutputTokens);

public sealed class AskService(RetrievalService retrieval, IChatClient chat, IOptions<RagOptions> options)
{
    public async Task<AskResult> AskAsync(string question, int? topK, CancellationToken ct)
    {
        var found = await retrieval.SearchAsync(question, topK, ct);
        var relevant = found.Where(c => c.Distance <= options.Value.MaxDistance).ToList();

        // Honest failure: nothing close enough means we don't ask the model to guess.
        if (relevant.Count == 0)
            return new AskResult(PromptBuilder.NoAnswer, [], null, null);

        var response = await chat.GetResponseAsync(
            PromptBuilder.Build(question, relevant),
            new ChatOptions { Temperature = 0.1f }, // low temperature = factual, repeatable answers
            ct);

        var answer = response.Text.Trim();
        var declined = answer.StartsWith(PromptBuilder.NoAnswer, StringComparison.OrdinalIgnoreCase);

        return new AskResult(
            answer,
            declined ? [] : ToSources(relevant), // no sources when the model itself said "I don't know"
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
    }

    private static List<SourceDto> ToSources(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks
            .Select((c, i) => new SourceDto(
                i + 1, c.DocumentId, c.FileName, c.ChunkIndex, Math.Round(c.Distance, 4), Snippet(c.Content)))
            .ToList();

    private static string Snippet(string content) =>
        content.Length <= 200 ? content : content[..200] + "…";
}