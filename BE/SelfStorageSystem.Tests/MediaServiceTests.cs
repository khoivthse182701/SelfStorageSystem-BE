using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class MediaServiceTests : IDisposable
{
    private readonly string _testWebRoot;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<ILogger<MediaService>> _loggerMock;
    private readonly MediaService _service;

    public MediaServiceTests()
    {
        _testWebRoot = Path.Combine(Path.GetTempPath(), "SelfStorageMediaTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWebRoot);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.WebRootPath).Returns(_testWebRoot);
        _envMock.Setup(e => e.ContentRootPath).Returns(_testWebRoot);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 51283);

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(context);

        _loggerMock = new Mock<ILogger<MediaService>>();

        _service = new MediaService(_envMock.Object, _httpContextAccessorMock.Object, _loggerMock.Object);
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

    private static IFormFile CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task UploadImage_ValidJpgFile_ShouldSaveFileAndReturnResponse()
    {
        // Arrange
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // Dummy JPG bytes
        var file = CreateMockFormFile("sample_photo.jpg", "image/jpeg", content);

        // Act
        var result = await _service.UploadImageAsync(file);

        // Assert
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
        // Arrange
        var file = CreateMockFormFile("empty.jpg", "image/jpeg", Array.Empty<byte>());

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_ShouldThrowValidationException()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4 };
        var file = CreateMockFormFile("virus.exe", "application/octet-stream", content);

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImage_FileTooLarge_ShouldThrowValidationException()
    {
        // Arrange: 11 MB file
        var largeContent = new byte[11 * 1024 * 1024];
        var file = CreateMockFormFile("giant.png", "image/png", largeContent);

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => _service.UploadImageAsync(file));
    }

    [Fact]
    public async Task UploadImages_MultipleValidFiles_ShouldReturnAllResponses()
    {
        // Arrange
        var file1 = CreateMockFormFile("photo1.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var file2 = CreateMockFormFile("photo2.webp", "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 });

        // Act
        var results = await _service.UploadImagesAsync(new[] { file1, file2 });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("photo1.png", results[0].OriginalName);
        Assert.Equal("photo2.webp", results[1].OriginalName);
    }
}
