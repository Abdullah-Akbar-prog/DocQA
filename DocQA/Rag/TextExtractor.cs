using System.Text;
using UglyToad.PdfPig;

namespace DocQA.Rag;

public interface ITextExtractor
{
    Task<string> ExtractAsync(string path, string extension, CancellationToken ct);
}

public sealed class TextExtractor : ITextExtractor
{
    public async Task<string> ExtractAsync(string path, string extension, CancellationToken ct)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".txt":
            case ".md":
                return await File.ReadAllTextAsync(path, ct);
            case ".pdf":
                return await Task.Run(() => ExtractPdf(path), ct);
            default:
                throw new NotSupportedException($"Unsupported file type '{extension}'.");
        }
    }

    private static string ExtractPdf(string path)
    {
        using var pdf = PdfDocument.Open(path);
        var text = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            text.AppendLine(page.Text);
            text.AppendLine();
        }
        return text.ToString();
    }
}