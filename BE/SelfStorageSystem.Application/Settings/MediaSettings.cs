namespace SelfStorageSystem.Application.Settings;

public class MediaSettings
{
    public const string SectionName = "MediaSettings";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    ];

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"
    ];

    public string UploadFolder { get; set; } = "uploads/media";
}
