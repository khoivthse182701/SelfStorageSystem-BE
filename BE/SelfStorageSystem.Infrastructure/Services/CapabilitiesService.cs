using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class CapabilitiesService(SelfStorageDbContext db) : ICapabilitiesService
{
    private static readonly IReadOnlyList<string> CustomerPermissions = new[]
    {
        "catalog:view",
        "reservation:create",
        "agreement:view",
        "unit:access",
        "unit:transfer",
        "unit:move_out",
        "invoice:view",
        "payment:create",
        "support:ticket"
    };

    private static readonly IReadOnlyList<string> StaffPermissions = new[]
    {
        "handover:check",
        "qr:verify",
        "qr:confirm",
        "unit:list",
        "unit:view",
        "unit:update_status",
        "unit:lock",
        "ticket:list",
        "ticket:assign",
        "ticket:propose_charge",
        "ticket:complete"
    };

    // Merged facility_manager permissions (includes previous business_operations_manager capabilities)
    private static readonly IReadOnlyList<string> ManagerPermissions = new[]
    {
        "facility:dashboard",
        "facility:rentals",
        "facility:units",
        "facility:unit_status",
        "facility:map_layout",
        "facility:transfer_requests",
        "facility:staff_list",
        "facility:staff_tasks",
        "facility:refund_approvals",
        "facility:charge_proposals",
        "facility:debts",
        // Merged from Operations:
        "pricing:rates_manage",
        "promotions:manage",
        "pricing:fees_view"
    };

    private static readonly IReadOnlyList<string> AdminPermissions = new[]
    {
        "*"
    };

    public async Task<UserCapabilitiesDto> GetUserCapabilitiesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.UserRoleUsers).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AdminErrors.UserNotFound);
        }

        var roles = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add(RoleConstants.Customer);
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            switch (role)
            {
                case RoleConstants.SystemAdministrator:
                    foreach (var p in AdminPermissions) permissions.Add(p);
                    break;
                case RoleConstants.FacilityManager:
                    foreach (var p in ManagerPermissions) permissions.Add(p);
                    break;
                case RoleConstants.FacilityStaff:
                    foreach (var p in StaffPermissions) permissions.Add(p);
                    break;
                case RoleConstants.Customer:
                    foreach (var p in CustomerPermissions) permissions.Add(p);
                    break;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var assignedFacilities = await db.StaffFacilityAssignments
            .AsNoTracking()
            .Where(a => a.EmployeeId == userId && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.FacilityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (assignedFacilities.Count == 0 && roles.Any(r => r.Contains("manager", StringComparison.OrdinalIgnoreCase) || r.Contains("admin", StringComparison.OrdinalIgnoreCase)))
        {
            assignedFacilities = await db.Facilities
                .AsNoTracking()
                .Where(f => f.Status == FacilityStatusConstants.Active)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);
        }

        return new UserCapabilitiesDto(
            UserId: user.Id,
            Email: user.Email,
            Roles: roles,
            Permissions: permissions.ToList(),
            AssignedFacilityIds: assignedFacilities.Count > 0 ? assignedFacilities : null
        );
    }
}
