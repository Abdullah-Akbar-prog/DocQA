using System.ComponentModel.DataAnnotations;

namespace DocQA.Models;

public sealed class AskRequest
{
    [Required, StringLength(2000)]
    public string Question { get; init; } = string.Empty;

    [Range(1, 20)]
    public int? TopK { get; init; }
}