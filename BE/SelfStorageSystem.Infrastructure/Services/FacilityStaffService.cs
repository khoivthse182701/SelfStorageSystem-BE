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

    public FacilityStaffService(
        SelfStorageDbContext dbContext,
        IOptions<PaymentSettings>? paymentOptions = null,
        IRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _paymentSettings = paymentOptions?.Value ?? new PaymentSettings();
        _realtimeNotifier = realtimeNotifier;
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
        if (facilityId.HasValue) return facilityId.Value;

        var now = DateTimeOffset.UtcNow;
        var assignedFacilityId = await _dbContext.StaffFacilityAssignments
            .Where(s => s.Employee.UserId == employeeUserId && (!s.EndsAt.HasValue || s.EndsAt > now))
            .Select(s => (long?)s.FacilityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignedFacilityId.HasValue) return assignedFacilityId.Value;

        throw AppException.FromError(ManagerErrors.EmployeeNotAssignedToFacility);
    }
}
