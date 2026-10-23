namespace SelfStorageSystem.Contracts.Auth;

public record UserCapabilitiesDto(
    long UserId,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<long>? AssignedFacilityIds
);
