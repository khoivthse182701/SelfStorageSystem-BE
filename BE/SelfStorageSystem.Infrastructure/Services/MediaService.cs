using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Media;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public class MediaService : IMediaService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MediaService> logger)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<UploadMediaResponse> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateImageFile(file);

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var dateFolder = DateTime.UtcNow.ToString("yyyyMMdd");
        var relativeFolder = Path.Combine("uploads", "media", dateFolder);
        var targetDirectory = Path.Combine(webRoot, relativeFolder);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalFilePath = Path.Combine(targetDirectory, uniqueFileName);

        await using (var stream = new FileStream(physicalFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativeUrl = $"/uploads/media/{dateFolder}/{uniqueFileName}";
        var fullUrl = BuildAbsoluteUrl(relativeUrl);

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
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        var results = new List<UploadMediaResponse>(files.Count);
        foreach (var file in files)
        {
            var result = await UploadImageAsync(file, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private static void ValidateImageFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw AppException.FromError(MediaErrors.FileTooLarge);
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw AppException.FromError(MediaErrors.InvalidFileType);
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            throw AppException.FromError(MediaErrors.InvalidFileType);
        }
    }

    private string BuildAbsoluteUrl(string relativeUrl)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}{relativeUrl}";
        }

        return relativeUrl;
    }
}
