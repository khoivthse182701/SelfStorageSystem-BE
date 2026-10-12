namespace SelfStorageSystem.Contracts.Media;

public class UploadMediaResponse
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
}
