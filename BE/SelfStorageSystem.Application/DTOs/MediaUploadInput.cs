namespace SelfStorageSystem.Application.DTOs;

public sealed record MediaUploadInput(
    string FileName,
    string ContentType,
    long Length,
    Stream Content,
    string? PublicBaseUrl = null);
