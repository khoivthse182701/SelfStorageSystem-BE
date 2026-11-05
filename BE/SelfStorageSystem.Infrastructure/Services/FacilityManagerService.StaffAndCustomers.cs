using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityManagerService : IFacilityManagerService
{
    // ==========================================

    // 6. Quản Lý Nhân Sự & Khách Hàng Cơ Sở (Facility Scope)

    // ==========================================



    public async Task<PagedResult<ManagerStaffItemDto>> GetFacilityStaffsAsync(

        long? facilityId,

        string? search,

        int pageNumber,

        int pageSize,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var now = DateTimeOffset.UtcNow;



        var query = _dbContext.StaffFacilityAssignments

            .AsNoTracking()

            .Include(sfa => sfa.Employee)

                .ThenInclude(e => e.User)

            .Include(sfa => sfa.Facility)

            .Where(sfa => sfa.FacilityId == targetFacilityId && (sfa.EndsAt == null || sfa.EndsAt > now));



        if (!string.IsNullOrWhiteSpace(search))

        {

            var s = search.Trim().ToLower();

            query = query.Where(sfa => sfa.Employee.FullName.ToLower().Contains(s)

                || sfa.Employee.EmployeeCode.ToLower().Contains(s)

                || sfa.Employee.User.Email.ToLower().Contains(s)

                || (sfa.Employee.User.PhoneNumber != null && sfa.Employee.User.PhoneNumber.Contains(s)));

        }



        var total = await query.CountAsync(cancellationToken);

        var items = await query

            .OrderByDescending(sfa => sfa.CreatedAt)

            .Skip((pageNumber - 1) * pageSize)

            .Take(pageSize)

            .Select(sfa => new ManagerStaffItemDto

            {

                UserId = sfa.Employee.UserId,

                EmployeeCode = sfa.Employee.EmployeeCode,

                FullName = sfa.Employee.FullName,

                Email = sfa.Employee.User.Email,

                PhoneNumber = sfa.Employee.User.PhoneNumber,

                HireDate = sfa.Employee.HireDate,

                EmploymentStatus = sfa.Employee.EmploymentStatus,

                UserStatus = sfa.Employee.User.Status,

                FacilityId = sfa.FacilityId,

                FacilityName = sfa.Facility.Name,

                AssignmentRole = sfa.AssignmentRole,

                CreatedAt = sfa.CreatedAt

            })

            .ToListAsync(cancellationToken);



        return new PagedResult<ManagerStaffItemDto>(items, total, pageNumber, pageSize);

    }



    public async Task<ManagerStaffItemDto> CreateFacilityStaffAsync(

        ManagerCreateStaffRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await ValidateFacilityAccessAsync(request.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (emailExists)

        {

            throw AppException.FromError(ManagerErrors.DuplicateEmail);

        }



        var now = DateTimeOffset.UtcNow;

        var user = new User

        {

            Email = email,

            PhoneNumber = request.PhoneNumber?.Trim(),

            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),

            Status = "active",

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(cancellationToken);



        var employeeCode = $"EMP-{now:yyMM}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        var hireDate = request.HireDate ?? DateOnly.FromDateTime(now.DateTime);



        var employee = new EmployeeProfile

        {

            UserId = user.Id,

            EmployeeCode = employeeCode,

            FullName = request.FullName.Trim(),

            HireDate = hireDate,

            EmploymentStatus = "active",

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.EmployeeProfiles.Add(employee);



        var assignment = new StaffFacilityAssignment

        {

            EmployeeId = user.Id,

            FacilityId = request.FacilityId,

            AssignmentRole = string.IsNullOrWhiteSpace(request.AssignmentRole) ? RoleConstants.FacilityStaff : request.AssignmentRole.Trim(),

            StartsAt = now,

            AssignedBy = currentUserId,

            CreatedAt = now

        };

        _dbContext.StaffFacilityAssignments.Add(assignment);



        await _dbContext.SaveChangesAsync(cancellationToken);



        var facility = await _dbContext.Facilities.FindAsync(new object[] { request.FacilityId }, cancellationToken);



        return new ManagerStaffItemDto

        {

            UserId = user.Id,

            EmployeeCode = employee.EmployeeCode,

            FullName = employee.FullName,

            Email = user.Email,

            PhoneNumber = user.PhoneNumber,

            HireDate = employee.HireDate,

            EmploymentStatus = employee.EmploymentStatus,

            UserStatus = user.Status,

            FacilityId = request.FacilityId,

            FacilityName = facility?.Name ?? string.Empty,

            AssignmentRole = assignment.AssignmentRole,

            CreatedAt = now

        };

    }



    public async Task<ManagerStaffItemDto> UpdateFacilityStaffAsync(

        long staffUserId,

        ManagerUpdateStaffRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var now = DateTimeOffset.UtcNow;

        var assignment = await _dbContext.StaffFacilityAssignments

            .Include(sfa => sfa.Facility)

            .Include(sfa => sfa.Employee)

                .ThenInclude(e => e.User)

            .FirstOrDefaultAsync(sfa => sfa.EmployeeId == staffUserId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);



        if (assignment == null)

        {

            throw AppException.FromError(ManagerErrors.StaffNotFound);

        }



        await ValidateFacilityAccessAsync(assignment.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var employee = assignment.Employee;

        employee.FullName = request.FullName.Trim();

        if (request.HireDate.HasValue) employee.HireDate = request.HireDate.Value;

        if (!string.IsNullOrWhiteSpace(request.EmploymentStatus)) employee.EmploymentStatus = request.EmploymentStatus.Trim();

        employee.UpdatedAt = now;



        if (request.PhoneNumber != null)

        {

            employee.User.PhoneNumber = request.PhoneNumber.Trim();

            employee.User.UpdatedAt = now;

        }



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerStaffItemDto

        {

            UserId = employee.UserId,

            EmployeeCode = employee.EmployeeCode,

            FullName = employee.FullName,

            Email = employee.User.Email,

            PhoneNumber = employee.User.PhoneNumber,

            HireDate = employee.HireDate,

            EmploymentStatus = employee.EmploymentStatus,

            UserStatus = employee.User.Status,

            FacilityId = assignment.FacilityId,

            FacilityName = assignment.Facility?.Name ?? string.Empty,

            AssignmentRole = assignment.AssignmentRole,

            CreatedAt = assignment.CreatedAt

        };

    }



    public async Task<ManagerStaffItemDto> UpdateFacilityStaffStatusAsync(

        long staffUserId,

        ManagerUpdateStaffStatusRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var now = DateTimeOffset.UtcNow;

        var assignment = await _dbContext.StaffFacilityAssignments

            .Include(sfa => sfa.Facility)

            .Include(sfa => sfa.Employee)

                .ThenInclude(e => e.User)

            .FirstOrDefaultAsync(sfa => sfa.EmployeeId == staffUserId, cancellationToken);



        if (assignment == null)

        {

            throw AppException.FromError(ManagerErrors.StaffNotFound);

        }



        await ValidateFacilityAccessAsync(assignment.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var isActive = request.Status.Equals("active", StringComparison.OrdinalIgnoreCase);

        assignment.Employee.User.Status = isActive ? "active" : "inactive";

        assignment.Employee.User.UpdatedAt = now;



        assignment.Employee.EmploymentStatus = isActive ? "active" : "terminated";

        assignment.Employee.UpdatedAt = now;



        if (!isActive)

        {

            assignment.EndsAt = now;

        }

        else if (assignment.EndsAt.HasValue && assignment.EndsAt <= now)

        {

            assignment.EndsAt = null;

        }



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerStaffItemDto

        {

            UserId = assignment.Employee.UserId,

            EmployeeCode = assignment.Employee.EmployeeCode,

            FullName = assignment.Employee.FullName,

            Email = assignment.Employee.User.Email,

            PhoneNumber = assignment.Employee.User.PhoneNumber,

            HireDate = assignment.Employee.HireDate,

            EmploymentStatus = assignment.Employee.EmploymentStatus,

            UserStatus = assignment.Employee.User.Status,

            FacilityId = assignment.FacilityId,

            FacilityName = assignment.Facility?.Name ?? string.Empty,

            AssignmentRole = assignment.AssignmentRole,

            CreatedAt = assignment.CreatedAt

        };

    }



    public async Task<PagedResult<ManagerCustomerItemDto>> GetFacilityCustomersAsync(

        long? facilityId,

        string? search,

        int pageNumber,

        int pageSize,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var agreementCustomers = _dbContext.RentalAgreements

            .Where(a => a.FacilityId == targetFacilityId)

            .Select(a => a.CustomerId);



        var reservationCustomers = _dbContext.Reservations

            .Where(r => r.FacilityId == targetFacilityId)

            .Select(r => r.CustomerId);



        var customerIdsQuery = agreementCustomers.Union(reservationCustomers);



        var customersQuery = _dbContext.CustomerProfiles

            .AsNoTracking()

            .Include(cp => cp.User)

            .Include(cp => cp.RentalAgreements)

            .Include(cp => cp.Reservations)

            .Where(cp => customerIdsQuery.Contains(cp.UserId));



        if (!string.IsNullOrWhiteSpace(search))

        {

            var s = search.Trim().ToLower();

            customersQuery = customersQuery.Where(cp => cp.FullName.ToLower().Contains(s)

                || cp.User.Email.ToLower().Contains(s)

                || (cp.User.PhoneNumber != null && cp.User.PhoneNumber.Contains(s)));

        }



        var total = await customersQuery.CountAsync(cancellationToken);

        var items = await customersQuery

            .OrderByDescending(cp => cp.CreatedAt)

            .Skip((pageNumber - 1) * pageSize)

            .Take(pageSize)

            .Select(cp => new ManagerCustomerItemDto

            {

                CustomerId = cp.UserId,

                FullName = cp.FullName,

                Email = cp.User.Email,

                PhoneNumber = cp.User.PhoneNumber,

                TotalAgreements = cp.RentalAgreements.Count(a => a.FacilityId == targetFacilityId),

                ActiveAgreements = cp.RentalAgreements.Count(a => a.FacilityId == targetFacilityId && (a.Status == "active" || a.Status == "expiring_soon")),

                TotalReservations = cp.Reservations.Count(r => r.FacilityId == targetFacilityId),

                FirstActivityAt = cp.CreatedAt,

                LastActivityAt = cp.UpdatedAt

            })

            .ToListAsync(cancellationToken);



        return new PagedResult<ManagerCustomerItemDto>(items, total, pageNumber, pageSize);

    }
}
