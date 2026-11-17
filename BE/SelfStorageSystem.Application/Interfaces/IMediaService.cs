using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Contracts.Media;

namespace SelfStorageSystem.Application.Interfaces;

public interface IMediaService
{
    Task<UploadMediaResponse> UploadImageAsync(MediaUploadInput file, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadMediaResponse>> UploadImagesAsync(IReadOnlyList<MediaUploadInput> files, CancellationToken cancellationToken = default);
}
