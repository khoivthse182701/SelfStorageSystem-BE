using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user, IEnumerable<string> roles, string? fullName = null);
}
