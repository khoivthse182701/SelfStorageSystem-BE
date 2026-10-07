using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class MediaErrors
{
    public static readonly Error NoFileProvided =
        Error.Validation("Media.NoFileProvided", "No file was uploaded or the uploaded file is empty.");

    public static readonly Error FileTooLarge =
        Error.Validation("Media.FileTooLarge", "File size exceeds the maximum allowed limit of 10 MB.");

    public static readonly Error InvalidFileType =
        Error.Validation("Media.InvalidFileType", "Only image files (.jpg, .jpeg, .png, .webp, .gif, .bmp) are allowed.");
}
