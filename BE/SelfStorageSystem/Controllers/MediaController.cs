using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Media;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/media")]
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
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await using var stream = file.OpenReadStream();
        var input = new MediaUploadInput(file.FileName, file.ContentType, file.Length, stream, baseUrl);
        var response = await _mediaService.UploadImageAsync(input, cancellationToken);
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
    public async Task<IActionResult> UploadMultiple(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            throw AppException.FromError(MediaErrors.NoFileProvided);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var inputs = new List<MediaUploadInput>(files.Count);
        var openedStreams = new List<Stream>(files.Count);
        try
        {
            foreach (var file in files)
            {
                var stream = file.OpenReadStream();
                openedStreams.Add(stream);
                inputs.Add(new MediaUploadInput(file.FileName, file.ContentType, file.Length, stream, baseUrl));
            }

            var response = await _mediaService.UploadImagesAsync(inputs, cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<UploadMediaResponse>>.Ok(response, $"{response.Count} images uploaded successfully."));
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
