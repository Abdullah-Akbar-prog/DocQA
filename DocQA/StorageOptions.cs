namespace DocQA;

public sealed class StorageOptions
{
    public string UploadPath { get; set; } = "uploads";
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
}