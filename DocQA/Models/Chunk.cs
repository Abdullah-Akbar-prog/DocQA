namespace DocQA.Models;
public class Chunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int Index { get; set; }
    public required string Content { get; set; }
    public required byte[] Embedding { get; set; }
    // The embedding column is added in step 7, with its own migration.
}