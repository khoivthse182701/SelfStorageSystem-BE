using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Media;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly MediaSettings _mediaSettings;
    private readonly ILogger<MediaService> _logger;
    private readonly HashSet<string> _allowedExtensions;
    private readonly HashSet<string> _allowedContentTypes;

    public MediaService(ILogger<MediaService> logger)
        : this(Options.Create(new MediaSettings()), logger)
    {
    }

    public MediaService(
        IOptions<MediaSettings>? mediaOptions,
        ILogger<MediaService> logger)
    {
        _mediaSettings = mediaOptions?.Value ?? new MediaSettings();
        _logger = logger;

        _allowedExtensions = new HashSet<string>(_mediaSettings.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        _allowedContentTypes = new HashSet<string>(_mediaSettings.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<UploadMediaResponse> UploadImageAsync(MediaUploadInput file, CancellationToken cancellationToken = default)
    {
        ValidateImageFile(file);

        var webRoot = string.IsNullOrWhiteSpace(_mediaSettings.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : _mediaSettings.WebRootPath;

        var dateFolder = DateTime.UtcNow.ToString("yyyyMMdd");
        var uploadFolder = !string.IsNullOrWhiteSpace(_mediaSettings.UploadFolder)
            ? _mediaSettings.UploadFolder.Trim('/', '\\')
            : "uploads/media";

        var targetDirectory = Path.Combine(webRoot, uploadFolder, dateFolder);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalFilePath = Path.Combine(targetDirectory, uniqueFileName);

        await using (var targetStream = new FileStream(physicalFilePath, FileMode.Create))
        {
            if (file.Content.CanSeek)
            {
                file.Content.Position = 0;
            }
            await file.Content.CopyToAsync(targetStream, cancellationToken);
        }

        var normalizedUploadFolder = uploadFolder.Replace('\\', '/');
        var relativeUrl = $"/{normalizedUploadFolder}/{dateFolder}/{uniqueFileName}";
        var fullUrl = BuildAbsoluteUrl(relativeUrl, file.PublicBaseUrl);

        _logger.LogInformation("Image uploaded successfully: {FileName} ({Size} bytes)", uniqueFileName, file.Length);

        return new UploadMediaResponse
        {
            FileName = uniqueFileName,
            OriginalName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Url = fullUrl
        };
    }

    public async Task<IReadOnlyList<UploadMediaResponse>> UploadImagesAsync(
        IReadOnlyList<MediaUploadInput> files,
        CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        var results = new List<UploadMediaResponse>(files.Count);
        foreach (var file in files)
        {
            results.Add(await UploadImageAsync(file, cancellationToken));
        }

        return results;
    }

    private void ValidateImageFile(MediaUploadInput? file)
    {
        if (file is null || file.Length == 0)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        if (file.Length > _mediaSettings.MaxFileSizeBytes)
        {
            throw AppException.FromError(MediaErrors.FileTooLarge);
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !_allowedExtensions.Contains(extension))
        {
            throw AppException.FromError(MediaErrors.InvalidFileType);
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !_allowedContentTypes.Contains(file.ContentType))
        {
            throw AppException.FromError(MediaErrors.InvalidFileType);
        }
    }

    private string BuildAbsoluteUrl(string relativeUrl, string? requestBaseUrl)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(requestBaseUrl)
            ? requestBaseUrl.TrimEnd('/')
            : _mediaSettings.PublicBaseUrl?.TrimEnd('/');

        return !string.IsNullOrWhiteSpace(baseUrl)
            ? $"{baseUrl}{relativeUrl}"
            : relativeUrl;
    }
}
