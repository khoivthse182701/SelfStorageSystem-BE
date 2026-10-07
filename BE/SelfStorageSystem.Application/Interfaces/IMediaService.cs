using Microsoft.AspNetCore.Http;
using SelfStorageSystem.Contracts.Media;

namespace SelfStorageSystem.Application.Interfaces;

public interface IMediaService
{
    Task<UploadMediaResponse> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadMediaResponse>> UploadImagesAsync(IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default);
}
