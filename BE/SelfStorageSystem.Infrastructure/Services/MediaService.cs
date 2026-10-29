using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Media;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly MediaSettings _mediaSettings;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MediaService> _logger;
    private readonly HashSet<string> _allowedExtensions;
    private readonly HashSet<string> _allowedContentTypes;

    public MediaService(
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MediaService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(new MediaSettings()), environment, httpContextAccessor, logger)
    {
    }

    public MediaService(
        IOptions<MediaSettings>? mediaOptions,
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MediaService> logger)
    {
        _mediaSettings = mediaOptions?.Value ?? new MediaSettings();
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        _allowedExtensions = new HashSet<string>(_mediaSettings.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        _allowedContentTypes = new HashSet<string>(_mediaSettings.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
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
        var uploadFolder = !string.IsNullOrWhiteSpace(_mediaSettings.UploadFolder)
            ? _mediaSettings.UploadFolder.Trim('/', '\\')
            : "uploads/media";

        var targetDirectory = Path.Combine(webRoot, uploadFolder, dateFolder);

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

        var normalizedUploadFolder = uploadFolder.Replace('\\', '/');
        var relativeUrl = $"/{normalizedUploadFolder}/{dateFolder}/{uniqueFileName}";
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

    private void ValidateImageFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
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
