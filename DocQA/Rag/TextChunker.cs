using System.Text.RegularExpressions;

namespace DocQA.Rag;

public static partial class TextChunker
{
    private static readonly char[] SentenceEnds = ['.', '!', '?', '\n'];

    /// <summary>
    /// Splits text into overlapping chunks of at most <paramref name="maxChars"/> characters,
    /// preferring to break at sentence ends, then at spaces. About 4 characters is one token.
    /// </summary>
    public static IReadOnlyList<string> Chunk(string text, int maxChars = 1800, int overlap = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        if (overlap >= maxChars)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be smaller than the chunk size.");

        text = Normalize(text);
        var chunks = new List<string>();
        if (text.Length == 0) return chunks;

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + maxChars, text.Length);

            if (end < text.Length)
                end = FindBreak(text, start, end);

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);

            if (end >= text.Length) break;

            // Step back by the overlap, but always move forward.
            var next = end - overlap;
            start = next > start ? next : end;
        }

        return chunks;
    }

    private static int FindBreak(string text, int start, int end)
    {
        // Only look in the second half of the window, so chunks don't become tiny.
        var window = (end - start) / 2;

        var sentence = text.LastIndexOfAny(SentenceEnds, end - 1, window);
        if (sentence >= 0) return sentence + 1;

        var space = text.LastIndexOf(' ', end - 1, window);
        return space >= 0 ? space : end;
    }

    private static string Normalize(string text) =>
        NewLines().Replace(Spaces().Replace(text.Replace("\r\n", "\n"), " "), "\n\n").Trim();

    [GeneratedRegex("[ \t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex("\n{3,}")]
    private static partial Regex NewLines();
}