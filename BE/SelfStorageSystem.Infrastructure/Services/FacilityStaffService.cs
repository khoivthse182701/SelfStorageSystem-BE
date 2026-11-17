using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityStaffService : IFacilityStaffService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly PaymentSettings _paymentSettings;
    private readonly IRealtimeNotifier? _realtimeNotifier;
    private readonly IQrTokenService? _qrTokenService;
    private readonly IEmailService? _emailService;

    public FacilityStaffService(
        SelfStorageDbContext dbContext,
        IOptions<PaymentSettings>? paymentOptions = null,
        IRealtimeNotifier? realtimeNotifier = null,
        IQrTokenService? qrTokenService = null,
        IEmailService? emailService = null)
    {
        _dbContext = dbContext;
        _paymentSettings = paymentOptions?.Value ?? new PaymentSettings();
        _realtimeNotifier = realtimeNotifier;
        _qrTokenService = qrTokenService;
        _emailService = emailService;
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task<EmployeeProfile> GetEmployeeProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (employee == null)
            throw AppException.FromError(StaffErrors.EmployeeProfileNotFound);

        return employee;
    }

    private async Task<long> ResolveFacilityIdAsync(long employeeUserId, long? facilityId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var isAdmin = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == employeeUserId &&
                (ur.Role.Code == RoleConstants.SystemAdministrator ||
                 ur.Role.Code == RoleConstants.Admin ||
                 ur.Role.Code == "admin"),
                cancellationToken);

        if (facilityId.HasValue)
        {
            if (isAdmin)
            {
                return facilityId.Value;
            }

            var isAssigned = await _dbContext.StaffFacilityAssignments
                .AnyAsync(s => s.EmployeeId == employeeUserId
                            && s.FacilityId == facilityId.Value
                            && (!s.EndsAt.HasValue || s.EndsAt > now),
                    cancellationToken);

            if (!isAssigned)
            {
                throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);
            }

            return facilityId.Value;
        }

        var assignedFacilityId = await _dbContext.StaffFacilityAssignments
            .Where(s => s.EmployeeId == employeeUserId
                     && (!s.EndsAt.HasValue || s.EndsAt > now))
            .Select(s => (long?)s.FacilityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignedFacilityId.HasValue)
        {
            return assignedFacilityId.Value;
        }

        if (isAdmin)
        {
            var fallbackFacilityId = await _dbContext.Facilities
                .Select(f => (long?)f.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (fallbackFacilityId.HasValue)
            {
                return fallbackFacilityId.Value;
            }
        }

        throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);
    }
}
