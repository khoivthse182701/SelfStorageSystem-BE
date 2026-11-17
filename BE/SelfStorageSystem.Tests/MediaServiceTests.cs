using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class MediaServiceTests : IDisposable
{
    private readonly string _testWebRoot;
    private readonly Mock<ILogger<MediaService>> _loggerMock;
    private readonly MediaService _service;

    public MediaServiceTests()
    {
        _testWebRoot = Path.Combine(Path.GetTempPath(), "SelfStorageMediaTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWebRoot);

        _loggerMock = new Mock<ILogger<MediaService>>();

        var settings = Options.Create(new MediaSettings
        {
            WebRootPath = _testWebRoot,
            PublicBaseUrl = "http://localhost:51283"
        });

        _service = new MediaService(settings, _loggerMock.Object);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWebRoot))
            {
                Directory.Delete(_testWebRoot, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temporary test directories
        }
    }

    private static MediaUploadInput CreateUploadInput(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new MediaUploadInput(fileName, contentType, content.Length, stream, "http://localhost:51283");
    }

    [Fact]
    public async Task UploadImage_ValidJpgFile_ShouldSaveFileAndReturnResponse()
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var file = CreateUploadInput("sample_photo.jpg", "image/jpeg", content);

        var result = await _service.UploadImageAsync(file);

        Assert.NotNull(result);
        Assert.Equal("sample_photo.jpg", result.OriginalName);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(content.Length, result.FileSizeBytes);
        Assert.EndsWith(".jpg", result.FileName);
        Assert.StartsWith("http://localhost:51283/uploads/media/", result.Url);
    }

    [Fact]
    public async Task UploadImage_EmptyOrNullFile_ShouldThrowValidationException()
    {
        var file = CreateUploadInput("empty.jpg", "image/jpeg", Array.Empty<byte>());
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_ShouldThrowValidationException()
    {
        var content = new byte[] { 1, 2, 3, 4 };
        var file = CreateUploadInput("virus.exe", "application/octet-stream", content);
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImage_FileTooLarge_ShouldThrowValidationException()
    {
        var largeContent = new byte[11 * 1024 * 1024];
        var file = CreateUploadInput("giant.png", "image/png", largeContent);
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImages_MultipleValidFiles_ShouldReturnAllResponses()
    {
        var file1 = CreateUploadInput("photo1.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var file2 = CreateUploadInput("photo2.webp", "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 });

        var results = await _service.UploadImagesAsync(new[] { file1, file2 });

        Assert.Equal(2, results.Count);
        Assert.Equal("photo1.png", results[0].OriginalName);
        Assert.Equal("photo2.webp", results[1].OriginalName);
    }

    [Fact]
    public async Task UploadImage_CustomMediaSettings_ShouldRespectConfiguredSizeAndFolder()
    {
        var customSettings = new MediaSettings
        {
            MaxFileSizeBytes = 500,
            UploadFolder = "custom/assets",
            WebRootPath = _testWebRoot,
            PublicBaseUrl = "http://localhost:51283"
        };

        var customService = new MediaService(Options.Create(customSettings), _loggerMock.Object);

        var oversizedFile = CreateUploadInput("too_big.jpg", "image/jpeg", new byte[600]);
        await Assert.ThrowsAsync<AppValidationException>(() => customService.UploadImageAsync(oversizedFile));

        var validFile = CreateUploadInput("small.jpg", "image/jpeg", new byte[300]);
        var result = await customService.UploadImageAsync(validFile);

        Assert.NotNull(result);
        Assert.Contains("/custom/assets/", result.Url);
    }
}
