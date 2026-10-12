using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Media;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    /// <summary>
    /// Upload a single image file (JPG, PNG, WEBP, GIF, BMP - max 10MB).
    /// Returns the hosted public image URL.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UploadMediaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        var response = await _mediaService.UploadImageAsync(file, cancellationToken);
        return Ok(ApiResponse<UploadMediaResponse>.Ok(response, "Image uploaded successfully."));
    }

    /// <summary>
    /// Upload multiple image files simultaneously.
    /// </summary>
    [HttpPost("upload-multiple")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UploadMediaResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadMultiple(List<IFormFile> files, CancellationToken cancellationToken)
    {
        var response = await _mediaService.UploadImagesAsync(files, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UploadMediaResponse>>.Ok(response, $"{response.Count} images uploaded successfully."));
    }
}
