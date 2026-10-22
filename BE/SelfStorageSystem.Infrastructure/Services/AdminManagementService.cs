using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class AdminManagementService : IAdminManagementService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly ILogger<AdminManagementService> _logger;

    public AdminManagementService(
        SelfStorageDbContext dbContext,
        ILogger<AdminManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ==========================================
    // 1. Quản lý Người Dùng & Phân Quyền (RBAC)
    // ==========================================

    public async Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        AdminUserQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? AdminConstants.DefaultPageNumber : query.PageNumber;
        var pageSize = query.PageSize < 1 ? AdminConstants.DefaultPageSize 
            : Math.Min(query.PageSize, AdminConstants.MaxPageSize);

        var usersQuery = _dbContext.Users
            .AsNoTracking()
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.EmployeeProfile)
                .ThenInclude(ep => ep!.StaffFacilityAssignments)
                    .ThenInclude(sfa => sfa.Facility)
            .AsQueryable();

        // 1. Search term filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            usersQuery = usersQuery.Where(u =>
                u.Email.ToLower().Contains(search) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)) ||
                (u.CustomerProfile != null && u.CustomerProfile.FullName.ToLower().Contains(search)) ||
                (u.EmployeeProfile != null && (
                    u.EmployeeProfile.FullName.ToLower().Contains(search) ||
                    u.EmployeeProfile.EmployeeCode.ToLower().Contains(search)
                )));
        }

        // 2. Status filter
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLower();
            usersQuery = usersQuery.Where(u => u.Status.ToLower() == status);
        }

        // 3. Role filter
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleCode = query.Role.Trim().ToLower();
            usersQuery = usersQuery.Where(u => u.UserRoleUsers.Any(ur => ur.Role.Code.ToLower() == roleCode));
        }

        // 4. FacilityId filter
        if (query.FacilityId.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            usersQuery = usersQuery.Where(u => u.EmployeeProfile != null &&
                u.EmployeeProfile.StaffFacilityAssignments.Any(sfa =>
                    sfa.FacilityId == query.FacilityId.Value &&
                    (sfa.EndsAt == null || sfa.EndsAt > now)));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTimeOffset.UtcNow;
        var items = users.Select(u =>
        {
            var fullName = u.EmployeeProfile?.FullName ?? u.CustomerProfile?.FullName ?? u.Email;
            var employeeCode = u.EmployeeProfile?.EmployeeCode;
            var roles = u.UserRoleUsers.Select(ur => ur.Role.Code).Distinct().ToList();

            var activeAssignments = u.EmployeeProfile?.StaffFacilityAssignments
                .Where(sfa => sfa.EndsAt == null || sfa.EndsAt > nowUtc)
                .ToList() ?? new List<StaffFacilityAssignment>();

            return new AdminUserSummaryDto
            {
                Id = u.Id,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                FullName = fullName,
                EmployeeCode = employeeCode,
                Roles = roles,
                AssignedFacilityNames = activeAssignments.Select(a => a.Facility.Name).Distinct().ToList(),
                AssignedFacilityIds = activeAssignments.Select(a => a.FacilityId).Distinct().ToList()
            };
        }).ToList();

        return new PagedResult<AdminUserSummaryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminUserDetailDto> GetUserDetailAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
                .ThenInclude(ep => ep!.StaffFacilityAssignments)
                    .ThenInclude(sfa => sfa.Facility)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.GrantedByNavigation)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AdminErrors.UserNotFound);
        }

        var recentLogins = await _dbContext.LoginHistories
            .AsNoTracking()
            .Where(lh => lh.UserId == id || lh.AttemptedEmail == user.Email)
            .OrderByDescending(lh => lh.OccurredAt)
            .Take(AdminConstants.RecentLoginsTake)
            .ToListAsync(cancellationToken);

        var roleDtos = user.UserRoleUsers.Select(ur => new AdminUserRoleDto
        {
            RoleId = ur.RoleId,
            Code = ur.Role.Code,
            DisplayName = ur.Role.DisplayName,
            GrantedAt = ur.GrantedAt,
            GrantedBy = ur.GrantedBy,
            GrantedByEmail = ur.GrantedByNavigation?.Email
        }).ToList();

        AdminCustomerProfileDto? customerProfileDto = null;
        if (user.CustomerProfile != null)
        {
            customerProfileDto = new AdminCustomerProfileDto
            {
                UserId = user.CustomerProfile.UserId,
                FullName = user.CustomerProfile.FullName,
                IdentityNumber = user.CustomerProfile.IdentityNumber,
                DateOfBirth = user.CustomerProfile.DateOfBirth,
                Address = user.CustomerProfile.Address,
                EmergencyContactName = user.CustomerProfile.EmergencyContactName,
                EmergencyContactPhone = user.CustomerProfile.EmergencyContactPhone,
                CreatedAt = user.CustomerProfile.CreatedAt
            };
        }

        AdminEmployeeProfileDto? employeeProfileDto = null;
        if (user.EmployeeProfile != null)
        {
            employeeProfileDto = new AdminEmployeeProfileDto
            {
                UserId = user.EmployeeProfile.UserId,
                EmployeeCode = user.EmployeeProfile.EmployeeCode,
                FullName = user.EmployeeProfile.FullName,
                HireDate = user.EmployeeProfile.HireDate,
                EmploymentStatus = user.EmployeeProfile.EmploymentStatus,
                CreatedAt = user.EmployeeProfile.CreatedAt,
                FacilityAssignments = user.EmployeeProfile.StaffFacilityAssignments.Select(sfa => new AdminStaffFacilityAssignmentDto
                {
                    Id = sfa.Id,
                    EmployeeId = sfa.EmployeeId,
                    EmployeeCode = user.EmployeeProfile.EmployeeCode,
                    EmployeeName = user.EmployeeProfile.FullName,
                    FacilityId = sfa.FacilityId,
                    FacilityCode = sfa.Facility.Code,
                    FacilityName = sfa.Facility.Name,
                    AssignmentRole = sfa.AssignmentRole,
                    StartsAt = sfa.StartsAt,
                    EndsAt = sfa.EndsAt,
                    AssignedBy = sfa.AssignedBy,
                    CreatedAt = sfa.CreatedAt
                }).ToList()
            };
        }

        var loginHistoryDtos = recentLogins.Select(lh => new AdminLoginHistoryDto
        {
            Id = lh.Id,
            UserId = lh.UserId,
            AttemptedEmail = lh.AttemptedEmail,
            Result = lh.Result,
            IpAddress = lh.IpAddress,
            UserAgent = lh.UserAgent,
            OccurredAt = lh.OccurredAt,
            IsSuspicious = lh.Result == LoginHistoryResultConstants.Locked || lh.Result == LoginHistoryResultConstants.Blocked,
            SuspiciousReason = lh.Result == LoginHistoryResultConstants.Locked 
                ? AdminConstants.SuspiciousLockedAttemptReason
                : lh.Result == LoginHistoryResultConstants.Blocked 
                    ? AdminConstants.SuspiciousBlockedAttemptReason 
                    : null
        }).ToList();

        return new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roleDtos,
            CustomerProfile = customerProfileDto,
            EmployeeProfile = employeeProfileDto,
            RecentLoginHistories = loginHistoryDtos
        };
    }

    public async Task<AdminUserDetailDto> CreateInternalUserAsync(
        AdminCreateUserRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Check existing email
        var exists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw AppException.FromError(AdminErrors.EmailAlreadyExists);
        }

        // 2. Validate password
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < AdminConstants.MinPasswordLength)
        {
            throw AppException.FromError(AdminErrors.PasswordRequired);
        }

        // 3. Validate role
        var normalizedRoleCode = request.Role.Trim().ToLowerInvariant();
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == normalizedRoleCode, cancellationToken);
        if (role == null || !AdminConstants.ValidInternalRoles.Contains(role.Code))
        {
            throw AppException.FromError(AdminErrors.InvalidRole);
        }

        // 4. Check facility if initial assignment requested
        Facility? initialFacility = null;
        if (request.InitialFacilityId.HasValue)
        {
            if (!AdminConstants.ValidStaffAssignmentRoles.Contains(role.Code))
            {
                throw AppException.FromError(AdminErrors.InvalidInitialFacilityRole);
            }

            initialFacility = await _dbContext.Facilities.FirstOrDefaultAsync(f => f.Id == request.InitialFacilityId.Value, cancellationToken);
            if (initialFacility == null)
            {
                throw AppException.FromError(AdminErrors.FacilityNotFound);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Generate unique employee code if not supplied
        var employeeCode = !string.IsNullOrWhiteSpace(request.EmployeeCode) 
            ? request.EmployeeCode.Trim().ToUpperInvariant() 
            : $"{AdminConstants.DefaultEmployeeCodePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % AdminConstants.AutoEmployeeCodeModulo:D6}";

        var codeExists = await _dbContext.EmployeeProfiles.AnyAsync(ep => ep.EmployeeCode == employeeCode, cancellationToken);
        if (codeExists)
        {
            throw AppException.FromError(AdminErrors.EmployeeCodeAlreadyExists);
        }

        var newUser = new User
        {
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber?.Trim(),
            PasswordHash = hashedPassword,
            Status = UserStatusConstants.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var employeeProfile = new EmployeeProfile
        {
            EmployeeCode = employeeCode,
            FullName = request.FullName.Trim(),
            HireDate = request.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            EmploymentStatus = EmploymentStatusConstants.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        newUser.EmployeeProfile = employeeProfile;

        newUser.UserRoleUsers.Add(new UserRole
        {
            Role = role,
            RoleId = role.Id,
            GrantedBy = adminUserId,
            GrantedAt = now
        });

        if (initialFacility != null && AdminConstants.ValidStaffAssignmentRoles.Contains(role.Code))
        {
            employeeProfile.StaffFacilityAssignments.Add(new StaffFacilityAssignment
            {
                FacilityId = initialFacility.Id,
                AssignmentRole = role.Code,
                StartsAt = now,
                AssignedBy = adminUserId,
                CreatedAt = now
            });
        }

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Record Audit Log
        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.User,
            entityId: newUser.Id.ToString(),
            action: AuditActionConstants.Create,
            oldValues: null,
            newValues: $"{newUser.Email} ({role.Code}) - Code: {employeeCode}",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} successfully created internal user {Email} with role {Role}.",
            adminUserId, newUser.Email, role.Code);

        return await GetUserDetailAsync(newUser.Id, cancellationToken);
    }

    public async Task<AdminUserSummaryDto> UpdateUserStatusAsync(
        long id,
        AdminUpdateUserStatusRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = request.Status.Trim().ToLowerInvariant();

        if (!AdminConstants.ValidUserStatuses.Contains(normalizedStatus))
        {
            throw AppException.FromError(AdminErrors.InvalidStatus);
        }

        if (id == adminUserId && normalizedStatus != UserStatusConstants.Active)
        {
            throw AppException.FromError(AdminErrors.CannotDeactivateSelf);
        }

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AdminErrors.UserNotFound);
        }

        var oldStatus = user.Status;
        var now = DateTimeOffset.UtcNow;

        user.Status = normalizedStatus;
        user.UpdatedAt = now;
        if (user.EmployeeProfile != null)
        {
            user.EmployeeProfile.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.User,
            entityId: user.Id.ToString(),
            action: AuditActionConstants.UpdateStatus,
            oldValues: oldStatus,
            newValues: $"{normalizedStatus} (Reason: {request.Reason ?? AdminConstants.DefaultNotApplicable})",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} changed status of user {UserId} from {Old} to {New}.",
            adminUserId, user.Id, oldStatus, normalizedStatus);

        return new AdminUserSummaryDto
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            FullName = user.EmployeeProfile?.FullName ?? user.CustomerProfile?.FullName ?? user.Email,
            EmployeeCode = user.EmployeeProfile?.EmployeeCode,
            Roles = user.UserRoleUsers.Select(ur => ur.Role.Code).Distinct().ToList()
        };
    }

    public async Task<IReadOnlyList<string>> UpdateUserRolesAsync(
        long id,
        AdminUpdateUserRolesRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AdminErrors.UserNotFound);
        }

        var requestedRoleCodes = request.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (requestedRoleCodes.Count == 0)
        {
            throw AppException.FromError(AdminErrors.InvalidRole);
        }

        // Prevent self-demotion
        if (id == adminUserId && !requestedRoleCodes.Contains(RoleConstants.SystemAdministrator))
        {
            throw AppException.FromError(AdminErrors.CannotDemoteSelf);
        }

        var dbRoles = await _dbContext.Roles
            .Where(r => requestedRoleCodes.Contains(r.Code.ToLower()))
            .ToListAsync(cancellationToken);

        if (dbRoles.Count != requestedRoleCodes.Count)
        {
            throw AppException.FromError(AdminErrors.InvalidRole);
        }

        var existingRoleCodes = user.UserRoleUsers.Select(ur => ur.Role.Code.ToLower()).ToList();

        // 1. Remove obsolete roles
        var rolesToRemove = user.UserRoleUsers
            .Where(ur => !requestedRoleCodes.Contains(ur.Role.Code.ToLower()))
            .ToList();

        foreach (var r in rolesToRemove)
        {
            _dbContext.UserRoles.Remove(r);
        }

        // 2. Add new roles
        var now = DateTimeOffset.UtcNow;
        foreach (var dbRole in dbRoles)
        {
            if (!existingRoleCodes.Contains(dbRole.Code.ToLower()))
            {
                user.UserRoleUsers.Add(new UserRole
                {
                    Role = dbRole,
                    RoleId = dbRole.Id,
                    GrantedBy = adminUserId,
                    GrantedAt = now
                });
            }
        }

        var hasEmployeeRole = requestedRoleCodes.Any(r => AdminConstants.ValidStaffAssignmentRoles.Contains(r));
        if (hasEmployeeRole && user.EmployeeProfile == null)
        {
            var employeeCode = $"{AdminConstants.DefaultEmployeeCodePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % AdminConstants.AutoEmployeeCodeModulo:D6}";
            user.EmployeeProfile = new EmployeeProfile
            {
                UserId = user.Id,
                EmployeeCode = employeeCode,
                FullName = user.CustomerProfile?.FullName ?? user.Email,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EmploymentStatus = EmploymentStatusConstants.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        user.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.User,
            entityId: user.Id.ToString(),
            action: AuditActionConstants.UpdateRoles,
            oldValues: string.Join(", ", existingRoleCodes),
            newValues: string.Join(", ", requestedRoleCodes),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} updated roles for user {UserId} to [{Roles}].",
            adminUserId, user.Id, string.Join(", ", requestedRoleCodes));

        return requestedRoleCodes;
    }

    // ==========================================
    // 2. Phân Bổ Nhân Sự Điểm Kho (Data Scope)
    // ==========================================

    public async Task<AdminStaffFacilityAssignmentDto> AssignStaffToFacilityAsync(
        AdminCreateStaffAssignmentRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedAssignmentRole = request.AssignmentRole.Trim().ToLowerInvariant();
        if (!AdminConstants.ValidStaffAssignmentRoles.Contains(normalizedAssignmentRole))
        {
            throw AppException.FromError(AdminErrors.InvalidAssignmentRole);
        }

        var startsAt = request.StartsAt ?? DateTimeOffset.UtcNow;
        if (request.EndsAt.HasValue && request.EndsAt.Value <= startsAt)
        {
            throw AppException.FromError(AdminErrors.InvalidAssignmentDates);
        }

        var employee = await _dbContext.EmployeeProfiles
            .Include(ep => ep.User)
            .FirstOrDefaultAsync(ep => ep.UserId == request.EmployeeId, cancellationToken);

        if (employee == null)
        {
            throw AppException.FromError(AdminErrors.EmployeeProfileNotFound);
        }

        if (employee.EmploymentStatus == EmploymentStatusConstants.Terminated ||
            employee.User.Status != UserStatusConstants.Active)
        {
            throw AppException.FromError(AdminErrors.EmployeeInactive);
        }

        var facility = await _dbContext.Facilities
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility == null)
        {
            throw AppException.FromError(AdminErrors.FacilityNotFound);
        }

        if (facility.Status != FacilityStatusConstants.Active)
        {
            throw AppException.FromError(AdminErrors.FacilityNotActive);
        }

        // Check if there is already an active assignment for this employee and facility
        var existingActive = await _dbContext.StaffFacilityAssignments
            .AnyAsync(sfa => sfa.EmployeeId == employee.UserId &&
                             sfa.FacilityId == facility.Id &&
                             (sfa.EndsAt == null || sfa.EndsAt > startsAt),
                      cancellationToken);

        if (existingActive)
        {
            throw AppException.FromError(AdminErrors.AssignmentAlreadyExists);
        }

        var now = DateTimeOffset.UtcNow;
        var assignment = new StaffFacilityAssignment
        {
            EmployeeId = employee.UserId,
            FacilityId = facility.Id,
            AssignmentRole = normalizedAssignmentRole,
            StartsAt = startsAt,
            EndsAt = request.EndsAt,
            AssignedBy = adminUserId,
            CreatedAt = now
        };

        _dbContext.StaffFacilityAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.StaffFacilityAssignment,
            entityId: assignment.Id.ToString(),
            action: AuditActionConstants.AssignStaff,
            oldValues: null,
            newValues: $"Staff: {employee.FullName} ({employee.EmployeeCode}) -> Facility: {facility.Name} ({normalizedAssignmentRole})",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} assigned staff {EmployeeId} to facility {FacilityId} as {Role}.",
            adminUserId, employee.UserId, facility.Id, normalizedAssignmentRole);

        return new AdminStaffFacilityAssignmentDto
        {
            Id = assignment.Id,
            EmployeeId = employee.UserId,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.FullName,
            FacilityId = facility.Id,
            FacilityCode = facility.Code,
            FacilityName = facility.Name,
            AssignmentRole = assignment.AssignmentRole,
            StartsAt = assignment.StartsAt,
            EndsAt = assignment.EndsAt,
            AssignedBy = assignment.AssignedBy,
            CreatedAt = assignment.CreatedAt
        };
    }

    public async Task RevokeStaffAssignmentAsync(
        long assignmentId,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.StaffFacilityAssignments
            .Include(sfa => sfa.Employee)
            .Include(sfa => sfa.Facility)
            .FirstOrDefaultAsync(sfa => sfa.Id == assignmentId, cancellationToken);

        if (assignment == null)
        {
            throw AppException.FromError(AdminErrors.AssignmentNotFound);
        }

        var summary = $"EmployeeId: {assignment.EmployeeId}, FacilityId: {assignment.FacilityId}, Role: {assignment.AssignmentRole}";

        _dbContext.StaffFacilityAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.StaffFacilityAssignment,
            entityId: assignmentId.ToString(),
            action: AuditActionConstants.RevokeStaffAssignment,
            oldValues: summary,
            newValues: AdminConstants.RevokedStatus,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} revoked staff assignment {AssignmentId} ({Summary}).",
            adminUserId, assignmentId, summary);
    }

    // ==========================================
    // 3. Bảo Mật & Kiểm Toán & Dashboard
    // ==========================================

    public async Task<PagedResult<AdminAuditLogDto>> GetAuditLogsAsync(
        AdminAuditLogQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? AdminConstants.DefaultPageNumber : query.PageNumber;
        var pageSize = query.PageSize < 1 ? AdminConstants.DefaultPageSize 
            : Math.Min(query.PageSize, AdminConstants.MaxPageSize);

        var auditQuery = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.ActorUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim().ToLower();
            auditQuery = auditQuery.Where(a => a.EntityType.ToLower() == entityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityId = query.EntityId.Trim();
            auditQuery = auditQuery.Where(a => a.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLower();
            auditQuery = auditQuery.Where(a => a.Action.ToLower() == action);
        }

        if (query.ActorUserId.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.ActorUserId == query.ActorUserId.Value);
        }

        if (query.FromDate.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.OccurredAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.OccurredAt <= query.ToDate.Value);
        }

        var totalCount = await auditQuery.CountAsync(cancellationToken);

        var logs = await auditQuery
            .OrderByDescending(a => a.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = logs.Select(a => new AdminAuditLogDto
        {
            Id = a.Id,
            ActorUserId = a.ActorUserId,
            ActorEmail = a.ActorUser?.Email,
            ActorRole = a.ActorRole,
            EntitySchema = a.EntitySchema,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Action = a.Action,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            RequestId = a.RequestId,
            IpAddress = a.IpAddress,
            OccurredAt = a.OccurredAt
        }).ToList();

        return new PagedResult<AdminAuditLogDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminLoginHistoryReportDto> GetLoginHistoriesAsync(
        AdminLoginHistoryQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? AdminConstants.DefaultPageNumber : query.PageNumber;
        var pageSize = query.PageSize < 1 ? AdminConstants.DefaultPageSize 
            : Math.Min(query.PageSize, AdminConstants.MaxPageSize);

        var baseQuery = _dbContext.LoginHistories
            .AsNoTracking()
            .Include(l => l.User)
            .AsQueryable();

        if (query.UserId.HasValue)
        {
            baseQuery = baseQuery.Where(l => l.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AttemptedEmail))
        {
            var email = query.AttemptedEmail.Trim().ToLower();
            baseQuery = baseQuery.Where(l => l.AttemptedEmail != null && l.AttemptedEmail.ToLower().Contains(email));
        }

        if (!string.IsNullOrWhiteSpace(query.Result))
        {
            var result = query.Result.Trim().ToLower();
            baseQuery = baseQuery.Where(l => l.Result.ToLower() == result);
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(l => l.OccurredAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(l => l.OccurredAt <= query.ToDate.Value);
        }

        // Summary metrics across the filtered window
        var totalAttempts = await baseQuery.CountAsync(cancellationToken);
        var succeededAttempts = await baseQuery.CountAsync(l => l.Result == LoginHistoryResultConstants.Succeeded, cancellationToken);
        var failedAttempts = await baseQuery.CountAsync(l => l.Result == LoginHistoryResultConstants.Failed, cancellationToken);
        var lockedAttempts = await baseQuery.CountAsync(l => l.Result == LoginHistoryResultConstants.Locked, cancellationToken);
        var blockedAttempts = await baseQuery.CountAsync(l => l.Result == LoginHistoryResultConstants.Blocked, cancellationToken);

        // Fetch recent failed login clusters to detect suspicious IP / email brute-force attempts
        var oneDayAgo = DateTimeOffset.UtcNow.AddMinutes(-AdminConstants.SuspiciousWindowMinutes);
        var suspiciousIpsOrEmails = await _dbContext.LoginHistories
            .AsNoTracking()
            .Where(l => l.OccurredAt >= oneDayAgo && l.Result == LoginHistoryResultConstants.Failed)
            .GroupBy(l => new { l.IpAddress, l.AttemptedEmail })
            .Where(g => g.Count() >= AdminConstants.ConsecutiveFailedThreshold)
            .Select(g => new { g.Key.IpAddress, g.Key.AttemptedEmail })
            .ToListAsync(cancellationToken);

        var suspiciousIpsSet = suspiciousIpsOrEmails
            .Where(x => !string.IsNullOrEmpty(x.IpAddress))
            .Select(x => x.IpAddress!)
            .ToHashSet();

        var suspiciousEmailsSet = suspiciousIpsOrEmails
            .Where(x => !string.IsNullOrEmpty(x.AttemptedEmail))
            .Select(x => x.AttemptedEmail!.ToLowerInvariant())
            .ToHashSet();

        var suspiciousIpsList = suspiciousIpsSet.ToList();
        var suspiciousEmailsList = suspiciousEmailsSet.ToList();

        var suspiciousQuery = baseQuery.Where(l =>
            l.Result == LoginHistoryResultConstants.Locked ||
            l.Result == LoginHistoryResultConstants.Blocked ||
            (l.Result == LoginHistoryResultConstants.Failed &&
                ((l.IpAddress != null && suspiciousIpsList.Contains(l.IpAddress)) ||
                 (l.AttemptedEmail != null && suspiciousEmailsList.Contains(l.AttemptedEmail.ToLower())))));

        var suspiciousAttemptsCount = await suspiciousQuery.CountAsync(cancellationToken);

        var pagedQuery = query.IsSuspiciousOnly == true ? suspiciousQuery : baseQuery;
        var filteredTotal = query.IsSuspiciousOnly == true ? suspiciousAttemptsCount : totalAttempts;

        // Materialize target page
        var histories = await pagedQuery
            .OrderByDescending(l => l.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = histories.Select(l =>
        {
            var isLocked = l.Result == LoginHistoryResultConstants.Locked;
            var isBlocked = l.Result == LoginHistoryResultConstants.Blocked;
            var isBruteForce = (l.IpAddress != null && suspiciousIpsSet.Contains(l.IpAddress)) ||
                               (l.AttemptedEmail != null && suspiciousEmailsSet.Contains(l.AttemptedEmail.ToLowerInvariant()));

            var isSuspicious = isLocked || isBlocked || (isBruteForce && l.Result == LoginHistoryResultConstants.Failed);

            string? suspiciousReason = null;
            if (isLocked)
                suspiciousReason = AdminConstants.SuspiciousLockedAttemptReason;
            else if (isBlocked)
                suspiciousReason = AdminConstants.SuspiciousBlockedAttemptReason;
            else if (isSuspicious)
                suspiciousReason = AdminConstants.SuspiciousMultipleFailedReason;

            return new AdminLoginHistoryDto
            {
                Id = l.Id,
                UserId = l.UserId,
                AttemptedEmail = l.AttemptedEmail,
                Result = l.Result,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                OccurredAt = l.OccurredAt,
                IsSuspicious = isSuspicious,
                SuspiciousReason = suspiciousReason
            };
        }).ToList();

        return new AdminLoginHistoryReportDto
        {
            Histories = new PagedResult<AdminLoginHistoryDto>(items, filteredTotal, pageNumber, pageSize),
            Summary = new AdminLoginSecuritySummaryDto
            {
                TotalAttempts = totalAttempts,
                SucceededAttempts = succeededAttempts,
                FailedAttempts = failedAttempts,
                LockedAttempts = lockedAttempts,
                BlockedAttempts = blockedAttempts,
                SuspiciousAttemptsCount = suspiciousAttemptsCount
            }
        };
    }

    public async Task<AdminDashboardOverviewDto> GetDashboardOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var firstDayThisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var firstDayLastMonth = firstDayThisMonth.AddMonths(-1);

        // 1. Revenue & Payment KPIs
        var successfulPayments = _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentConstants.StatusSucceeded);

        var totalRevenue = await successfulPayments.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var thisMonthRevenue = await successfulPayments
            .Where(p => p.PaidAt >= firstDayThisMonth || (p.PaidAt == null && p.CreatedAt >= firstDayThisMonth))
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var lastMonthRevenue = await successfulPayments
            .Where(p => (p.PaidAt >= firstDayLastMonth && p.PaidAt < firstDayThisMonth) ||
                        (p.PaidAt == null && p.CreatedAt >= firstDayLastMonth && p.CreatedAt < firstDayThisMonth))
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var totalInvoices = await _dbContext.Invoices.CountAsync(cancellationToken);
        var paidInvoices = await _dbContext.Invoices.CountAsync(i => i.Status == InvoiceStatusConstants.Paid, cancellationToken);
        var overdueInvoices = await _dbContext.Invoices.CountAsync(i => i.Status == InvoiceStatusConstants.Overdue, cancellationToken);
        var openInvoices = await _dbContext.Invoices.CountAsync(i => i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.PartiallyPaid, cancellationToken);

        // 2. Capacity & Occupancy KPIs
        var totalFacilities = await _dbContext.Facilities.CountAsync(cancellationToken);
        var totalUnits = await _dbContext.StorageUnits.CountAsync(cancellationToken);
        var occupiedUnits = await _dbContext.StorageUnits.CountAsync(u => u.PhysicalStatus == StorageUnitStatusConstants.Occupied, cancellationToken);
        var reservedUnits = await _dbContext.StorageUnits.CountAsync(u => u.PhysicalStatus == StorageUnitStatusConstants.Reserved, cancellationToken);
        var availableUnits = await _dbContext.StorageUnits.CountAsync(u => u.PhysicalStatus == StorageUnitStatusConstants.Available, cancellationToken);
        var maintenanceUnits = await _dbContext.StorageUnits.CountAsync(u =>
            u.PhysicalStatus == StorageUnitStatusConstants.Maintenance ||
            u.PhysicalStatus == StorageUnitStatusConstants.OutOfService ||
            u.PhysicalStatus == StorageUnitStatusConstants.PendingInspection, cancellationToken);

        var systemOccupancyRate = totalUnits > 0
            ? Math.Round((double)(occupiedUnits + reservedUnits) / totalUnits * 100.0, 2)
            : 0.0;

        // 3. User & Staff KPIs
        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
        var totalCustomers = await _dbContext.CustomerProfiles.CountAsync(cancellationToken);
        var totalStaff = await _dbContext.EmployeeProfiles.CountAsync(cancellationToken);
        var activeRentals = await _dbContext.RentalAgreements.CountAsync(r => r.Status == RentalAgreementStatusConstants.Active, cancellationToken);

        // 4. Facility Breakdown
        var facilities = await _dbContext.Facilities
            .AsNoTracking()
            .Include(f => f.StorageUnits)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var facilityBreakdown = facilities.Select(f =>
        {
            var fTotalUnits = f.StorageUnits.Count;
            var fOccupied = f.StorageUnits.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Occupied);
            var fReserved = f.StorageUnits.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Reserved);
            var fAvailable = f.StorageUnits.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Available);
            var fRate = fTotalUnits > 0
                ? Math.Round((double)(fOccupied + fReserved) / fTotalUnits * 100.0, 2)
                : 0.0;

            return new AdminFacilityOccupancyBreakdownDto
            {
                FacilityId = f.Id,
                FacilityCode = f.Code,
                FacilityName = f.Name,
                City = f.City,
                Status = f.Status,
                TotalUnits = fTotalUnits,
                OccupiedUnits = fOccupied,
                ReservedUnits = fReserved,
                AvailableUnits = fAvailable,
                OccupancyRate = fRate
            };
        }).ToList();

        return new AdminDashboardOverviewDto
        {
            RevenueKpi = new AdminRevenueKpiDto
            {
                TotalRevenue = totalRevenue,
                ThisMonthRevenue = thisMonthRevenue,
                LastMonthRevenue = lastMonthRevenue,
                TotalInvoices = totalInvoices,
                PaidInvoices = paidInvoices,
                OverdueInvoices = overdueInvoices,
                OpenInvoices = openInvoices
            },
            OccupancyKpi = new AdminOccupancyKpiDto
            {
                TotalFacilities = totalFacilities,
                TotalStorageUnits = totalUnits,
                OccupiedUnits = occupiedUnits,
                ReservedUnits = reservedUnits,
                AvailableUnits = availableUnits,
                MaintenanceUnits = maintenanceUnits,
                SystemOccupancyRate = systemOccupancyRate
            },
            UserKpi = new AdminUserKpiDto
            {
                TotalUsers = totalUsers,
                TotalCustomers = totalCustomers,
                TotalStaff = totalStaff,
                ActiveRentals = activeRentals
            },
            FacilityBreakdown = facilityBreakdown
        };
    }

    // ==========================================
    // 4. Quản lý Cơ Sở (Facilities)
    // ==========================================

    public async Task<PagedResult<AdminFacilitySummaryDto>> GetFacilitiesAsync(
        AdminFacilityQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? AdminConstants.DefaultPageNumber : query.PageNumber;
        var pageSize = query.PageSize < 1 ? AdminConstants.DefaultPageSize
            : Math.Min(query.PageSize, AdminConstants.MaxPageSize);

        var facilitiesQuery = _dbContext.Facilities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            facilitiesQuery = facilitiesQuery.Where(f =>
                f.Code.ToLower().Contains(search) ||
                f.Name.ToLower().Contains(search) ||
                f.AddressLine.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            if (!AdminFacilityValidation.ValidStatuses.Contains(status))
            {
                throw AppException.FromError(AdminErrors.InvalidFacilityStatus);
            }
            facilitiesQuery = facilitiesQuery.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            facilitiesQuery = facilitiesQuery.Where(f => f.City.ToLower().Contains(city));
        }

        var totalCount = await facilitiesQuery.CountAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var items = await facilitiesQuery
            .OrderBy(f => f.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new AdminFacilitySummaryDto
            {
                Id = f.Id,
                Code = f.Code,
                Name = f.Name,
                AddressLine = f.AddressLine,
                District = f.District,
                City = f.City,
                Status = f.Status,
                OpeningTime = f.OpeningTime,
                ClosingTime = f.ClosingTime,
                TotalUnits = f.StorageUnits.Count(),
                ActiveStaffCount = f.StaffFacilityAssignments
                    .Where(sfa => sfa.StartsAt <= now && (sfa.EndsAt == null || sfa.EndsAt > now))
                    .Select(sfa => sfa.EmployeeId)
                    .Distinct()
                    .Count(),
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminFacilitySummaryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminFacilityDetailDto> GetFacilityDetailAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var facility = await _dbContext.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (facility == null)
        {
            throw AppException.FromError(AdminErrors.FacilityNotFound);
        }

        var unitStatusCounts = await _dbContext.StorageUnits
            .AsNoTracking()
            .Where(u => u.FacilityId == id)
            .GroupBy(u => u.PhysicalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(params string[] statuses) =>
            unitStatusCounts.Where(x => statuses.Contains(x.Status)).Sum(x => x.Count);

        var totalUnits = unitStatusCounts.Sum(x => x.Count);
        var occupied = CountOf(StorageUnitStatusConstants.Occupied);
        var reserved = CountOf(StorageUnitStatusConstants.Reserved);

        var now = DateTimeOffset.UtcNow;
        var assignments = await _dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .Include(sfa => sfa.Employee)
            .Include(sfa => sfa.AssignedByNavigation)
            .Where(sfa => sfa.FacilityId == id && (sfa.EndsAt == null || sfa.EndsAt > now))
            .OrderBy(sfa => sfa.AssignmentRole)
            .ThenBy(sfa => sfa.Employee.FullName)
            .ToListAsync(cancellationToken);

        return new AdminFacilityDetailDto
        {
            Id = facility.Id,
            Code = facility.Code,
            Name = facility.Name,
            AddressLine = facility.AddressLine,
            Ward = facility.Ward,
            District = facility.District,
            City = facility.City,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            Timezone = facility.Timezone,
            OpeningTime = facility.OpeningTime,
            ClosingTime = facility.ClosingTime,
            Status = facility.Status,
            CreatedAt = facility.CreatedAt,
            UpdatedAt = facility.UpdatedAt,
            UnitStats = new AdminFacilityUnitStatsDto
            {
                TotalUnits = totalUnits,
                Available = CountOf(StorageUnitStatusConstants.Available),
                Reserved = reserved,
                Occupied = occupied,
                Maintenance = CountOf(
                    StorageUnitStatusConstants.Maintenance,
                    StorageUnitStatusConstants.OutOfService,
                    StorageUnitStatusConstants.PendingInspection),
                OccupancyRate = totalUnits > 0
                    ? Math.Round((double)(occupied + reserved) / totalUnits * 100.0, 2)
                    : 0.0
            },
            AssignedStaff = assignments.Select(sfa => new AdminStaffFacilityAssignmentDto
            {
                Id = sfa.Id,
                EmployeeId = sfa.EmployeeId,
                EmployeeCode = sfa.Employee.EmployeeCode,
                EmployeeName = sfa.Employee.FullName,
                FacilityId = facility.Id,
                FacilityCode = facility.Code,
                FacilityName = facility.Name,
                AssignmentRole = sfa.AssignmentRole,
                StartsAt = sfa.StartsAt,
                EndsAt = sfa.EndsAt,
                AssignedBy = sfa.AssignedBy,
                AssignedByEmail = sfa.AssignedByNavigation?.Email,
                CreatedAt = sfa.CreatedAt
            }).ToList()
        };
    }

    public async Task<AdminFacilityDetailDto> CreateFacilityAsync(
        AdminCreateFacilityRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var (code, status, timezone) = NormalizeFacilityInput(request);

        if (await _dbContext.Facilities.AnyAsync(f => f.Code == code, cancellationToken))
        {
            throw AppException.FromError(AdminErrors.DuplicateFacilityCode);
        }

        var now = DateTimeOffset.UtcNow;
        var facility = new Facility
        {
            Code = code,
            Status = status,
            Timezone = timezone,
            CreatedAt = now
        };
        ApplyFacilityFields(facility, request, now);

        _dbContext.Facilities.Add(facility);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.Facility,
            entityId: facility.Id.ToString(),
            action: AuditActionConstants.Create,
            oldValues: null,
            newValues: DescribeFacility(facility),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} created facility {FacilityId} ({Code}).",
            adminUserId, facility.Id, facility.Code);

        return await GetFacilityDetailAsync(facility.Id, cancellationToken);
    }

    public async Task<AdminFacilityDetailDto> UpdateFacilityAsync(
        long id,
        AdminUpdateFacilityRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var facility = await _dbContext.Facilities.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (facility == null)
        {
            throw AppException.FromError(AdminErrors.FacilityNotFound);
        }

        var (code, status, timezone) = NormalizeFacilityInput(request);

        if (await _dbContext.Facilities.AnyAsync(f => f.Code == code && f.Id != id, cancellationToken))
        {
            throw AppException.FromError(AdminErrors.DuplicateFacilityCode);
        }

        var oldValues = DescribeFacility(facility);
        var now = DateTimeOffset.UtcNow;

        facility.Code = code;
        facility.Status = status;
        facility.Timezone = timezone;
        ApplyFacilityFields(facility, request, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditLogAsync(
            actorUserId: adminUserId,
            actorRole: RoleConstants.SystemAdministrator,
            entityType: AuditEntityTypeConstants.Facility,
            entityId: facility.Id.ToString(),
            action: AuditActionConstants.Update,
            oldValues: oldValues,
            newValues: DescribeFacility(facility),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Admin {AdminId} updated facility {FacilityId}.", adminUserId, facility.Id);

        return await GetFacilityDetailAsync(facility.Id, cancellationToken);
    }

    public async Task<AdminFacilityDetailDto> UpdateFacilityStatusAsync(
        long id,
        AdminUpdateFacilityStatusRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var status = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AdminFacilityValidation.ValidStatuses.Contains(status))
        {
            throw AppException.FromError(AdminErrors.InvalidFacilityStatus);
        }

        var facility = await _dbContext.Facilities.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (facility == null)
        {
            throw AppException.FromError(AdminErrors.FacilityNotFound);
        }

        var oldStatus = facility.Status;
        if (oldStatus != status)
        {
            facility.Status = status;
            facility.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecordAuditLogAsync(
                actorUserId: adminUserId,
                actorRole: RoleConstants.SystemAdministrator,
                entityType: AuditEntityTypeConstants.Facility,
                entityId: facility.Id.ToString(),
                action: AuditActionConstants.UpdateStatus,
                oldValues: oldStatus,
                newValues: $"{status} (Reason: {request.Reason ?? AdminConstants.DefaultNotApplicable})",
                cancellationToken: cancellationToken);

            _logger.LogInformation("Admin {AdminId} changed facility {FacilityId} status from {Old} to {New}.",
                adminUserId, facility.Id, oldStatus, status);
        }

        return await GetFacilityDetailAsync(facility.Id, cancellationToken);
    }

    private static (string Code, string Status, string Timezone) NormalizeFacilityInput(AdminCreateFacilityRequest request)
    {
        var status = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AdminFacilityValidation.ValidStatuses.Contains(status))
        {
            throw AppException.FromError(AdminErrors.InvalidFacilityStatus);
        }

        var timezone = (request.Timezone ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(timezone) || !AdminFacilityValidation.IsValidTimezone(timezone))
        {
            throw AppException.FromError(AdminErrors.InvalidTimezone);
        }

        return (request.Code.Trim().ToUpperInvariant(), status, timezone);
    }

    private static void ApplyFacilityFields(Facility facility, AdminCreateFacilityRequest request, DateTimeOffset now)
    {
        facility.Name = request.Name.Trim();
        facility.AddressLine = request.AddressLine.Trim();
        facility.Ward = string.IsNullOrWhiteSpace(request.Ward) ? null : request.Ward.Trim();
        facility.District = string.IsNullOrWhiteSpace(request.District) ? null : request.District.Trim();
        facility.City = request.City.Trim();
        facility.Latitude = request.Latitude;
        facility.Longitude = request.Longitude;
        facility.OpeningTime = request.OpeningTime;
        facility.ClosingTime = request.ClosingTime;
        facility.UpdatedAt = now;
    }

    private static string DescribeFacility(Facility f) =>
        $"Code: {f.Code}, Name: {f.Name}, Address: {f.AddressLine}, {f.District}, {f.City}, " +
        $"GPS: {f.Latitude},{f.Longitude}, TZ: {f.Timezone}, Hours: {f.OpeningTime}-{f.ClosingTime}, Status: {f.Status}";

    // ==========================================
    // Helper Audit Logger
    // ==========================================

    private async Task RecordAuditLogAsync(
        long? actorUserId,
        string? actorRole,
        string entityType,
        string entityId,
        string action,
        string? oldValues,
        string? newValues,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                ActorUserId = actorUserId,
                ActorRole = actorRole,
                EntitySchema = AdminConstants.DefaultAuditSchema,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ipAddress,
                OccurredAt = DateTimeOffset.UtcNow
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log for action {Action} on {EntityType} {EntityId}.",
                action, entityType, entityId);
        }
    }
}
