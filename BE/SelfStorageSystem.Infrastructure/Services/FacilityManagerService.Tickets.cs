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

    // 7. Quản Lý Sự Cố Ticket & Phân Công Kiểm Tra Hết Hạn

    // ==========================================



    public async Task<PagedResult<ManagerTicketDetailDto>> GetFacilityTicketsAsync(

        long? facilityId,

        string? status,

        string? category,

        int pageNumber,

        int pageSize,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var query = _dbContext.SupportTickets

            .AsNoTracking()

            .Include(t => t.Facility)

            .Include(t => t.Customer)

            .Include(t => t.StorageUnit)

            .Include(t => t.Agreement)

            .Include(t => t.TicketAssignment)

                .ThenInclude(ta => ta!.Employee)

            .Where(t => t.FacilityId == targetFacilityId);



        if (!string.IsNullOrWhiteSpace(status))

        {

            var s = status.Trim().ToLowerInvariant();

            query = query.Where(t => t.Status.ToLower() == s);

        }



        if (!string.IsNullOrWhiteSpace(category))

        {

            var c = category.Trim().ToLowerInvariant();

            query = query.Where(t => t.Category.ToLower() == c);

        }



        var total = await query.CountAsync(cancellationToken);

        var items = await query

            .OrderByDescending(t => t.CreatedAt)

            .Skip((pageNumber - 1) * pageSize)

            .Take(pageSize)

            .Select(t => new ManagerTicketDetailDto

            {

                Id = t.Id,

                TicketNo = t.TicketNo,

                FacilityId = t.FacilityId,

                FacilityName = t.Facility.Name,

                CustomerId = t.CustomerId,

                CustomerName = t.Customer.FullName,

                AgreementId = t.AgreementId,

                AgreementNo = t.Agreement != null ? t.Agreement.AgreementNo : null,

                StorageUnitId = t.StorageUnitId,

                UnitCode = t.StorageUnit != null ? t.StorageUnit.UnitCode : null,

                Category = t.Category,

                Priority = t.Priority,

                Subject = t.Subject,

                Description = t.Description,

                Status = t.Status,

                AssignedEmployeeId = t.TicketAssignment != null ? (long?)t.TicketAssignment.EmployeeId : null,

                AssignedEmployeeName = t.TicketAssignment != null ? t.TicketAssignment.Employee.FullName : null,

                AssignedAt = t.TicketAssignment != null ? (DateTimeOffset?)t.TicketAssignment.AssignedAt : null,

                Resolution = t.Resolution,

                ResolvedAt = t.ResolvedAt,

                CreatedAt = t.CreatedAt,

                UpdatedAt = t.UpdatedAt

            })

            .ToListAsync(cancellationToken);



        if (items.Count > 0)

        {

            var ticketIds = items.Select(x => x.Id).ToList();

            var assignments = await _dbContext.TicketAssignments

                .AsNoTracking()

                .Include(ta => ta.Employee)

                .Where(ta => ticketIds.Contains(ta.TicketId))

                .OrderByDescending(ta => ta.AssignedAt)

                .ToListAsync(cancellationToken);



            var staffTasks = await _dbContext.StaffTasks

                .AsNoTracking()

                .Include(st => st.AssignedEmployee)

                .Where(st => st.TicketId.HasValue && ticketIds.Contains(st.TicketId.Value))

                .OrderByDescending(st => st.UpdatedAt)

                .ToListAsync(cancellationToken);



            foreach (var item in items)

            {

                if (string.IsNullOrWhiteSpace(item.AssignedEmployeeName))

                {

                    var ta = assignments.FirstOrDefault(a => a.TicketId == item.Id);

                    if (ta != null && ta.Employee != null)

                    {

                        item.AssignedEmployeeId = ta.EmployeeId;

                        item.AssignedEmployeeName = ta.Employee.FullName;

                        item.AssignedAt = ta.AssignedAt;

                    }

                    else

                    {

                        var task = staffTasks.FirstOrDefault(st => st.TicketId == item.Id);

                        if (task != null && task.AssignedEmployee != null)

                        {

                            item.AssignedEmployeeId = task.AssignedEmployeeId;

                            item.AssignedEmployeeName = task.AssignedEmployee.FullName;

                            item.AssignedAt = task.CreatedAt;

                        }

                    }

                }

            }

        }



        return new PagedResult<ManagerTicketDetailDto>(items, total, pageNumber, pageSize);

    }



    public async Task<ManagerTicketDetailDto> CreateTicketAsync(

        ManagerCreateTicketRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await ValidateFacilityAccessAsync(request.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        long customerId = request.CustomerId ?? 1;



        if (request.AgreementId.HasValue && request.AgreementId.Value > 0)

        {

            var agr = await _dbContext.RentalAgreements.FirstOrDefaultAsync(a => a.Id == request.AgreementId.Value, cancellationToken);

            if (agr != null) customerId = agr.CustomerId;

        }



        var ticket = new SupportTicket

        {

            TicketNo = TicketConstants.GenerateTicketNo(),

            FacilityId = request.FacilityId,

            CustomerId = customerId,

            AgreementId = request.AgreementId > 0 ? request.AgreementId : null,

            StorageUnitId = request.StorageUnitId > 0 ? request.StorageUnitId : null,

            Category = string.IsNullOrWhiteSpace(request.Category) ? "incident" : request.Category.Trim().ToLowerInvariant(),

            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "normal" : request.Priority.Trim().ToLowerInvariant(),

            Subject = request.Subject.Trim(),

            Description = request.Description.Trim(),

            Status = request.AssignToEmployeeId.HasValue && request.AssignToEmployeeId.Value > 0

                ? TicketStatusConstants.Assigned

                : TicketStatusConstants.Open,

            CreatedAt = now,

            UpdatedAt = now

        };



        _dbContext.SupportTickets.Add(ticket);

        await _dbContext.SaveChangesAsync(cancellationToken);



        if (request.AssignToEmployeeId.HasValue && request.AssignToEmployeeId.Value > 0)

        {

            var assignRequest = new ManagerAssignTicketStaffRequest

            {

                EmployeeId = request.AssignToEmployeeId.Value,

                Notes = "Manager tạo ticket và giao việc trực tiếp.",

                DueAt = now.AddHours(24)

            };

            return await AssignTicketStaffAsync(ticket.Id, assignRequest, currentUserId, isSystemAdmin, cancellationToken);

        }



        if (_realtimeNotifier != null)

        {

            try

            {

                await _realtimeNotifier.SendTicketUpdateAsync(ticket.FacilityId, new

                {

                    ticketId = ticket.Id,

                    ticketNo = ticket.TicketNo,

                    status = ticket.Status,

                    subject = ticket.Subject,

                    facilityId = ticket.FacilityId

                });

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Failed to broadcast realtime ticket creation {TicketId}", ticket.Id);

            }

        }



        var customer = await _dbContext.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == customerId, cancellationToken);

        var unit = request.StorageUnitId.HasValue ? await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == request.StorageUnitId.Value, cancellationToken) : null;

        var agrRef = request.AgreementId.HasValue ? await _dbContext.RentalAgreements.FirstOrDefaultAsync(a => a.Id == request.AgreementId.Value, cancellationToken) : null;

        var fac = await _dbContext.Facilities.FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);



        return new ManagerTicketDetailDto

        {

            Id = ticket.Id,

            TicketNo = ticket.TicketNo,

            FacilityId = ticket.FacilityId,

            FacilityName = fac?.Name ?? string.Empty,

            CustomerId = ticket.CustomerId,

            CustomerName = customer?.FullName ?? string.Empty,

            AgreementId = ticket.AgreementId,

            AgreementNo = agrRef?.AgreementNo,

            StorageUnitId = ticket.StorageUnitId,

            UnitCode = unit?.UnitCode,

            Category = ticket.Category,

            Priority = ticket.Priority,

            Subject = ticket.Subject,

            Description = ticket.Description,

            Status = ticket.Status,

            CreatedAt = ticket.CreatedAt,

            UpdatedAt = ticket.UpdatedAt

        };

    }



    public async Task<ManagerTicketDetailDto> AssignTicketStaffAsync(

        long ticketId,

        ManagerAssignTicketStaffRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var ticket = await _dbContext.SupportTickets

            .Include(t => t.Facility)

            .Include(t => t.Customer)

            .Include(t => t.StorageUnit)

            .Include(t => t.Agreement)

            .Include(t => t.TicketAssignment)

                .ThenInclude(ta => ta!.Employee)

            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);



        if (ticket == null)

            throw AppException.FromError(ManagerErrors.TicketNotFound);



        await ValidateFacilityAccessAsync(ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var staffAssigned = await _dbContext.StaffFacilityAssignments

            .AnyAsync(sfa => sfa.EmployeeId == request.EmployeeId && sfa.FacilityId == ticket.FacilityId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);



        if (!staffAssigned)

            throw AppException.FromError(ManagerErrors.StaffNotAssignedToFacility);



        if (ticket.TicketAssignment == null)

        {

            ticket.TicketAssignment = new TicketAssignment

            {

                TicketId = ticket.Id,

                EmployeeId = request.EmployeeId,

                AssignedBy = currentUserId,

                AssignedAt = now

            };

            _dbContext.TicketAssignments.Add(ticket.TicketAssignment);

        }

        else

        {

            ticket.TicketAssignment.EmployeeId = request.EmployeeId;

            ticket.TicketAssignment.AssignedBy = currentUserId;

            ticket.TicketAssignment.AssignedAt = now;

            ticket.TicketAssignment.EndedAt = null;

        }



        ticket.Status = TicketStatusConstants.Assigned;

        ticket.UpdatedAt = now;



        var staffTask = new StaffTask

        {

            FacilityId = ticket.FacilityId,

            AssignedEmployeeId = request.EmployeeId,

            TicketId = ticket.Id,

            TaskType = "support",

            Title = $"[Sự cố #{ticket.TicketNo}] {ticket.Subject}",

            Status = "todo",

            CreatedBy = currentUserId,

            DueAt = request.DueAt ?? now.AddHours(24),

            ProgressPercent = 0,

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.StaffTasks.Add(staffTask);



        await _dbContext.SaveChangesAsync(cancellationToken);



        if (_realtimeNotifier != null)

        {

            try

            {

                await _realtimeNotifier.SendNotificationToUserAsync(

                    request.EmployeeId,

                    "Phân công sự cố mới",

                    $"Bạn vừa được Quản lý phân công xử lý Ticket #{ticket.TicketNo}: {ticket.Subject}",

                    new { ticketId = ticket.Id, ticketNo = ticket.TicketNo, facilityId = ticket.FacilityId }

                );

                await _realtimeNotifier.SendTicketUpdateAsync(ticket.FacilityId, new

                {

                    ticketId = ticket.Id,

                    ticketNo = ticket.TicketNo,

                    status = ticket.Status,

                    assignedEmployeeId = request.EmployeeId

                });

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Failed to send realtime notification for ticket assignment {TicketId}", ticket.Id);

            }

        }



        var employee = await _dbContext.EmployeeProfiles.FindAsync(new object[] { request.EmployeeId }, cancellationToken);



        return new ManagerTicketDetailDto

        {

            Id = ticket.Id,

            TicketNo = ticket.TicketNo,

            FacilityId = ticket.FacilityId,

            FacilityName = ticket.Facility.Name,

            CustomerId = ticket.CustomerId,

            CustomerName = ticket.Customer.FullName,

            AgreementId = ticket.AgreementId,

            AgreementNo = ticket.Agreement?.AgreementNo,

            StorageUnitId = ticket.StorageUnitId,

            UnitCode = ticket.StorageUnit?.UnitCode,

            Category = ticket.Category,

            Priority = ticket.Priority,

            Subject = ticket.Subject,

            Description = ticket.Description,

            Status = ticket.Status,

            AssignedEmployeeId = request.EmployeeId,

            AssignedEmployeeName = employee?.FullName,

            AssignedAt = now,

            Resolution = ticket.Resolution,

            ResolvedAt = ticket.ResolvedAt,

            CreatedAt = ticket.CreatedAt,

            UpdatedAt = ticket.UpdatedAt

        };

    }



    public async Task<IReadOnlyList<StaffTicketMessageDto>> GetTicketMessagesAsync(

        long ticketId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var ticket = await _dbContext.SupportTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null) throw AppException.FromError(ManagerErrors.TicketNotFound);



        await EnsureFacilityAccessAsync(ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var messages = await _dbContext.TicketMessages

            .AsNoTracking()

            .Include(m => m.AuthorUser)

                .ThenInclude(u => u.EmployeeProfile)

            .Include(m => m.AuthorUser)

                .ThenInclude(u => u.CustomerProfile)

            .Include(m => m.AuthorUser)

                .ThenInclude(u => u.UserRoleUsers)

                    .ThenInclude(ur => ur.Role)

            .Where(m => m.TicketId == ticketId)

            .OrderBy(m => m.CreatedAt)

            .ToListAsync(cancellationToken);



        return messages.Select(msg =>

        {

            var authorName = msg.AuthorUser?.EmployeeProfile?.FullName

                ?? msg.AuthorUser?.CustomerProfile?.FullName

                ?? msg.AuthorUser?.Email

                ?? "Hệ thống";



            var roleCode = msg.AuthorUser?.UserRoleUsers.FirstOrDefault()?.Role?.Code ?? "User";

            var roleDisplayName = roleCode switch

            {

                RoleConstants.FacilityStaff => "Nhân viên vận hành",

                RoleConstants.FacilityManager => "Quản lý cơ sở",

                RoleConstants.SystemAdministrator => "Quản trị viên",

                RoleConstants.Customer => "Khách hàng",

                _ => roleCode

            };



            return new StaffTicketMessageDto(

                msg.Id,

                ticketId,

                msg.AuthorUserId,

                authorName,

                roleDisplayName,

                msg.Body,

                msg.IsInternal,

                msg.CreatedAt

            );

        }).ToList();

    }



    public async Task<IReadOnlyList<ManagerChargeProposalDto>> GetTicketChargeProposalsAsync(

        long ticketId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var ticket = await _dbContext.SupportTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null) throw AppException.FromError(ManagerErrors.TicketNotFound);



        await EnsureFacilityAccessAsync(ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var proposals = await _dbContext.TicketChargeProposals

            .AsNoTracking()

            .Include(p => p.Ticket)

                .ThenInclude(t => t.Customer)

            .Include(p => p.Ticket)

                .ThenInclude(t => t.Agreement)

            .Include(p => p.ProposedByNavigation)

            .Include(p => p.TicketChargeApproval)

            .Where(p => p.TicketId == ticketId)

            .OrderByDescending(p => p.CreatedAt)

            .Select(p => new ManagerChargeProposalDto

            {

                Id = p.Id,

                TicketId = p.TicketId,

                TicketTitle = p.Ticket.Subject,

                CustomerId = p.Ticket.CustomerId,

                CustomerName = p.Ticket.Customer.FullName,

                AgreementId = p.Ticket.AgreementId,

                AgreementNo = p.Ticket.Agreement != null ? p.Ticket.Agreement.AgreementNo : null,

                ProposedBy = p.ProposedBy,

                ProposedByStaffName = p.ProposedByNavigation.FullName,

                Description = p.Description,

                Amount = p.Amount,

                Status = p.Status,

                CreatedAt = p.CreatedAt,

                Decision = p.TicketChargeApproval != null ? p.TicketChargeApproval.Decision : null,

                DecisionReason = p.TicketChargeApproval != null ? p.TicketChargeApproval.Reason : null,

                DecidedAt = p.TicketChargeApproval != null ? p.TicketChargeApproval.DecidedAt : null

            })

            .ToListAsync(cancellationToken);



        return proposals;

    }



    public async Task<ManagerTicketDetailDto> CloseTicketAsync(

        long ticketId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var ticket = await _dbContext.SupportTickets

            .Include(t => t.Facility)

            .Include(t => t.Customer)

            .Include(t => t.StorageUnit)

            .Include(t => t.Agreement)

            .Include(t => t.TicketAssignment)

                .ThenInclude(ta => ta!.Employee)

            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);



        if (ticket == null)

            throw AppException.FromError(ManagerErrors.TicketNotFound);



        await EnsureFacilityAccessAsync(ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        ticket.Status = TicketStatusConstants.Closed;

        ticket.UpdatedAt = now;



        await _dbContext.SaveChangesAsync(cancellationToken);



        // Retrieve latest assigned employee if available

        string? assignedName = ticket.TicketAssignment?.Employee?.FullName;

        long? assignedId = ticket.TicketAssignment?.EmployeeId;

        DateTimeOffset? assignedAt = ticket.TicketAssignment?.AssignedAt;



        if (string.IsNullOrWhiteSpace(assignedName))

        {

            var latestTa = await _dbContext.TicketAssignments

                .Include(ta => ta.Employee)

                .Where(ta => ta.TicketId == ticket.Id)

                .OrderByDescending(ta => ta.AssignedAt)

                .FirstOrDefaultAsync(cancellationToken);



            if (latestTa != null)

            {

                assignedId = latestTa.EmployeeId;

                assignedName = latestTa.Employee?.FullName;

                assignedAt = latestTa.AssignedAt;

            }

        }



        return new ManagerTicketDetailDto

        {

            Id = ticket.Id,

            TicketNo = ticket.TicketNo,

            FacilityId = ticket.FacilityId,

            FacilityName = ticket.Facility?.Name ?? "Facility",

            CustomerId = ticket.CustomerId,

            CustomerName = ticket.Customer?.FullName ?? "Customer",

            AgreementId = ticket.AgreementId,

            AgreementNo = ticket.Agreement?.AgreementNo,

            StorageUnitId = ticket.StorageUnitId,

            UnitCode = ticket.StorageUnit?.UnitCode,

            Category = ticket.Category,

            Priority = ticket.Priority,

            Subject = ticket.Subject,

            Description = ticket.Description,

            Status = ticket.Status,

            AssignedEmployeeId = assignedId,

            AssignedEmployeeName = assignedName,

            AssignedAt = assignedAt,

            Resolution = ticket.Resolution,

            ResolvedAt = ticket.ResolvedAt,

            CreatedAt = ticket.CreatedAt,

            UpdatedAt = ticket.UpdatedAt

        };

    }



    public async Task<IReadOnlyList<ManagerAgreementDetailDto>> GetExpiringOrMoveOutAgreementsAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await ValidateFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var today = DateOnly.FromDateTime(now.DateTime);

        var upcomingThreshold = today.AddDays(14);



        var agreements = await _dbContext.RentalAgreements

            .AsNoTracking()

            .Include(a => a.Customer)

                .ThenInclude(c => c.User)

            .Include(a => a.MoveOutRequest)

            .Include(a => a.UnitAllocations)

                .ThenInclude(ua => ua.StorageUnit)

            .Include(a => a.Invoices)

            .Where(a => a.FacilityId == facilityId &&

                (a.Status == "active" || a.Status == "expiring_soon" || a.Status == "overdue") &&

                (a.EndDate <= upcomingThreshold || (a.MoveOutRequest != null && (a.MoveOutRequest.Status == "pending" || a.MoveOutRequest.Status == "approved"))))

            .OrderBy(a => a.EndDate)

            .ToListAsync(cancellationToken);



        return agreements.Select(a =>

        {

            var activeAllocation = a.UnitAllocations.FirstOrDefault(ua => ua.Status == "active");

            return new ManagerAgreementDetailDto

            {

                Id = a.Id,

                AgreementNo = a.AgreementNo,

                CustomerId = a.CustomerId,

                CustomerName = a.Customer.FullName,

                CustomerPhone = a.Customer.User.PhoneNumber,

                CustomerEmail = a.Customer.User.Email,

                StorageUnitId = activeAllocation?.StorageUnitId ?? 0,

                UnitCode = activeAllocation?.StorageUnit?.UnitCode ?? string.Empty,

                StartDate = a.StartDate,

                EndDate = a.EndDate,

                MonthlyRent = a.MonthlyRateSnapshot,

                DepositAmount = a.DepositSnapshot,

                Status = a.Status,

                TotalDebt = a.Invoices.Where(i => i.Status == InvoiceStatusConstants.Overdue).Sum(i => i.TotalAmount - i.PaidAmount)

            };

        }).ToList();

    }



    public async Task<ManagerInspectionDetailDto> AssignMoveOutInspectionAsync(

        ManagerAssignInspectionRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var agreement = await _dbContext.RentalAgreements

            .Include(a => a.Facility)

            .Include(a => a.UnitAllocations)

                .ThenInclude(ua => ua.StorageUnit)

            .FirstOrDefaultAsync(a => a.Id == request.AgreementId, cancellationToken);



        if (agreement == null)

            throw AppException.FromError(ManagerErrors.AgreementNotFound);



        await ValidateFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var staffAssigned = await _dbContext.StaffFacilityAssignments

            .AnyAsync(sfa => (sfa.EmployeeId == request.EmployeeId || sfa.Employee.UserId == request.EmployeeId) 

                && sfa.FacilityId == agreement.FacilityId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);



        if (!staffAssigned)

            throw AppException.FromError(ManagerErrors.StaffNotAssignedToFacility);



        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == "active")

            ?? agreement.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();

        var activeUnit = activeAlloc?.StorageUnit;

        var storageUnitId = activeAlloc?.StorageUnitId ?? (agreement.UnitAllocations.FirstOrDefault()?.StorageUnitId);



        var inspection = new Inspection

        {

            FacilityId = agreement.FacilityId,

            AgreementId = agreement.Id,

            StorageUnitId = storageUnitId,

            InspectedBy = request.EmployeeId,

            InspectionType = "check_out",

            Status = InspectionStatusConstants.Draft,

            OverallCondition = "good",

            Summary = request.Notes,

            CreatedAt = now

        };

        _dbContext.Inspections.Add(inspection);



        if (agreement.Status == RentalAgreementStatusConstants.Active ||

            agreement.Status == RentalAgreementStatusConstants.ExpiringSoon ||

            agreement.Status == RentalAgreementStatusConstants.Overdue)

        {

            agreement.Status = RentalAgreementStatusConstants.MoveOutScheduled;

        }

        if (agreement.MoveOutRequest != null)

        {

            agreement.MoveOutRequest.Status = MoveOutStatusConstants.InspectionPending;

            agreement.MoveOutRequest.UpdatedAt = now;

        }

        agreement.UpdatedAt = now;



        var staffTask = new StaffTask

        {

            FacilityId = agreement.FacilityId,

            AssignedEmployeeId = request.EmployeeId,

            TaskType = "inspection",

            Title = $"[Nghiệm thu trả kho HĐ #{agreement.AgreementNo}] Ô kho {activeUnit?.UnitCode ?? ""}",

            Status = "todo",

            CreatedBy = currentUserId,

            DueAt = request.ScheduledAt ?? now.AddHours(24),

            ProgressPercent = 0,

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.StaffTasks.Add(staffTask);



        await _dbContext.SaveChangesAsync(cancellationToken);



        var employee = await _dbContext.EmployeeProfiles.FindAsync(new object[] { request.EmployeeId }, cancellationToken);



        return new ManagerInspectionDetailDto

        {

            Id = inspection.Id,

            FacilityId = inspection.FacilityId,

            FacilityName = agreement.Facility.Name,

            AgreementId = agreement.Id,

            AgreementNo = agreement.AgreementNo,

            StorageUnitId = activeUnit?.Id,

            UnitCode = activeUnit?.UnitCode,

            InspectedBy = request.EmployeeId,

            InspectedByName = employee?.FullName ?? string.Empty,

            InspectionType = inspection.InspectionType,

            Status = inspection.Status,

            Summary = inspection.Summary,

            CreatedAt = now

        };

    }



    private async Task<long> ResolveAndValidateFacilityIdAsync(

        long? facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken)

    {

        if (facilityId.HasValue)

        {

            await ValidateFacilityAccessAsync(facilityId.Value, currentUserId, isSystemAdmin, cancellationToken);

            return facilityId.Value;

        }



        var facilities = await GetMyFacilitiesAsync(currentUserId, isSystemAdmin, cancellationToken);

        if (facilities.Count == 0)

        {

            throw AppException.FromError(ManagerErrors.NotAssignedToFacility);

        }

        return facilities[0].Id;

    }



    private static (string? BankName, string? AccountNumber, string? AccountHolder, string? Note) ExtractBankInfo(string? reason)

    {

        if (string.IsNullOrWhiteSpace(reason)) return (null, null, null, null);

        try

        {

            if (reason.TrimStart().StartsWith("{"))

            {

                using var doc = System.Text.Json.JsonDocument.Parse(reason);

                var root = doc.RootElement;

                var bank = root.TryGetProperty("bank", out var b) ? b.GetString() : null;

                var acc = root.TryGetProperty("acc", out var a) ? a.GetString() : null;

                var holder = root.TryGetProperty("holder", out var h) ? h.GetString() : null;

                var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;

                return (bank, acc, holder, note);

            }

        }

        catch { }



        var bankMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Ngân hàng|BANK)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var accMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:STK|ACC)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var holderMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Chủ TK|NAME|HOLDER)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var noteMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Ghi chú|NOTE)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);



        return (

            bankMatch.Success ? bankMatch.Groups[1].Value.Trim() : null,

            accMatch.Success ? accMatch.Groups[1].Value.Trim() : null,

            holderMatch.Success ? holderMatch.Groups[1].Value.Trim() : null,

            noteMatch.Success ? noteMatch.Groups[1].Value.Trim() : null

        );

    }



    public async Task<PagedResult<ManagerRefundItemDto>> GetFacilityRefundsAsync(

        long? facilityId,

        string? status,

        int pageNumber,

        int pageSize,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var query = _dbContext.Refunds

            .AsNoTracking()

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Customer)

                    .ThenInclude(c => c.User)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Facility)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Inspections)

                    .ThenInclude(i => i.InspectionItems)

            .Include(r => r.RefundApproval)

                .ThenInclude(ra => ra!.DecidedByNavigation)

            .Where(r => r.Agreement != null && r.Agreement.FacilityId == targetFacilityId);



        if (!string.IsNullOrWhiteSpace(status))

        {

            var normalizedStatus = status.Trim().ToLowerInvariant();

            query = query.Where(r => r.Status == normalizedStatus);

        }



        var totalItems = await query.CountAsync(cancellationToken);



        var refunds = await query

            .OrderByDescending(r => r.CreatedAt)

            .Skip((pageNumber - 1) * pageSize)

            .Take(pageSize)

            .ToListAsync(cancellationToken);



        var items = refunds.Select(r =>

        {

            var agreement = r.Agreement;

            var deposit = agreement?.DepositSnapshot ?? 0m;

            var latestInspection = agreement?.Inspections.OrderByDescending(i => i.InspectedAt ?? i.CreatedAt).FirstOrDefault();

            var damageCost = latestInspection?.InspectionItems.Sum(ii => ii.ChargeAmount) ?? Math.Max(0m, deposit - r.Amount);



            var (bankName, accNumber, holder, note) = ExtractBankInfo(r.Reason);



            string? proofImageUrl = null;

            string? approvalNote = note;

            string? transactionRef = r.ProviderRefundId;



            if (!string.IsNullOrWhiteSpace(r.RefundApproval?.Reason))

            {

                try

                {

                    if (r.RefundApproval.Reason.TrimStart().StartsWith("{"))

                    {

                        using var doc = System.Text.Json.JsonDocument.Parse(r.RefundApproval.Reason);

                        var root = doc.RootElement;

                        if (root.TryGetProperty("proofImageUrl", out var p)) proofImageUrl = p.GetString();

                        if (root.TryGetProperty("note", out var n)) approvalNote = n.GetString();

                        if (root.TryGetProperty("transactionRef", out var t) && string.IsNullOrEmpty(transactionRef)) transactionRef = t.GetString();

                    }

                    else

                    {

                        approvalNote = r.RefundApproval.Reason;

                    }

                }

                catch { }

            }



            return new ManagerRefundItemDto

            {

                RefundId = r.Id,

                AgreementId = r.AgreementId,

                AgreementNo = agreement?.AgreementNo,

                FacilityId = agreement?.FacilityId ?? targetFacilityId,

                FacilityName = agreement?.Facility?.Name ?? string.Empty,

                CustomerId = agreement?.CustomerId ?? r.RequestedBy ?? 0,

                CustomerName = agreement?.Customer?.FullName ?? string.Empty,

                CustomerPhone = agreement?.Customer?.User?.PhoneNumber ?? string.Empty,

                CustomerEmail = agreement?.Customer?.User?.Email ?? string.Empty,

                DepositAmount = deposit,

                DamageCost = damageCost,

                RefundAmount = r.Amount,

                Currency = r.Currency,

                Status = r.Status,

                PayoutMethod = r.Provider,

                BankName = bankName,

                BankAccountNumber = accNumber,

                BankAccountHolder = holder,

                TransactionReference = transactionRef,

                ProofImageUrl = proofImageUrl,

                ProcessedByUserId = r.RefundApproval?.DecidedBy,

                ProcessedByUserName = r.RefundApproval?.DecidedByNavigation?.FullName,

                ProcessedAt = r.RefundApproval?.DecidedAt ?? r.RefundedAt,

                Note = approvalNote,

                CreatedAt = r.CreatedAt,

                RefundedAt = r.RefundedAt

            };

        }).ToList();



        return new PagedResult<ManagerRefundItemDto>(items, totalItems, pageNumber, pageSize);

    }



    public async Task<ManagerProcessRefundPayoutResultDto> ProcessRefundPayoutAsync(

        long refundId,

        ManagerProcessRefundPayoutRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var refund = await _dbContext.Refunds

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Customer)

                    .ThenInclude(c => c.User)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Facility)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.MoveOutRequest)

            .Include(r => r.RefundApproval)

            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);



        if (refund == null)

            throw AppException.FromError(ManagerErrors.RefundNotFound);



        if (refund.Agreement == null)

            throw AppException.FromError(ManagerErrors.AgreementNotFound);



        await ValidateFacilityAccessAsync(refund.Agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        if (refund.Amount <= 0m)

            throw AppException.FromError(ManagerErrors.RefundAmountInvalid);



        if (refund.Status == RefundStatusConstants.Completed)

            throw AppException.FromError(ManagerErrors.RefundAlreadyCompleted);



        var now = DateTimeOffset.UtcNow;



        var txRef = !string.IsNullOrWhiteSpace(request.TransactionReference)

            ? request.TransactionReference.Trim()

            : (!string.IsNullOrWhiteSpace(request.TransactionRef)

                ? request.TransactionRef.Trim()

                : $"FT{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");



        var proofImg = !string.IsNullOrWhiteSpace(request.ProofImageUrl)

            ? request.ProofImageUrl.Trim()

            : request.ProofUrl?.Trim();



        var noteText = !string.IsNullOrWhiteSpace(request.Note)

            ? request.Note.Trim()

            : request.Notes?.Trim();



        refund.Status = RefundStatusConstants.Completed;

        refund.Provider = RefundPayoutMethodConstants.BankTransfer;

        refund.ProviderRefundId = txRef;

        refund.RefundedAt = now;

        refund.UpdatedAt = now;



        var approvalReason = System.Text.Json.JsonSerializer.Serialize(new

        {

            payoutMethod = RefundPayoutMethodConstants.BankTransfer,

            transactionRef = txRef,

            proofImageUrl = proofImg,

            note = noteText

        });



        if (refund.RefundApproval == null)

        {

            refund.RefundApproval = new RefundApproval

            {

                RefundId = refund.Id,

                Decision = "completed",

                DecidedBy = currentUserId,

                Reason = approvalReason,

                DecidedAt = now

            };

            _dbContext.RefundApprovals.Add(refund.RefundApproval);

        }

        else

        {

            refund.RefundApproval.Decision = "completed";

            refund.RefundApproval.DecidedBy = currentUserId;

            refund.RefundApproval.Reason = approvalReason;

            refund.RefundApproval.DecidedAt = now;

        }



        // Complete Agreement & MoveOutRequest

        refund.Agreement.Status = RentalAgreementStatusConstants.Completed;

        refund.Agreement.DepositBalance = 0m;

        refund.Agreement.UpdatedAt = now;



        if (refund.Agreement.MoveOutRequest != null)

        {

            refund.Agreement.MoveOutRequest.Status = MoveOutStatusConstants.Completed;

            refund.Agreement.MoveOutRequest.FinalizedAt = now;

            refund.Agreement.MoveOutRequest.UpdatedAt = now;

        }



        // Notify Customer

        var customerUserId = refund.Agreement.Customer?.UserId;

        if (customerUserId.HasValue)

        {

            var (bankName, accNumber, _, _) = ExtractBankInfo(refund.Reason);

            var bankTarget = !string.IsNullOrEmpty(accNumber) ? $"{bankName} ({accNumber})" : "tài khoản ngân hàng của bạn";

            _dbContext.Notifications.Add(new Notification

            {

                UserId = customerUserId.Value,

                Channel = "in_app",

                TemplateCode = "REFUND_PAYOUT_COMPLETED",

                Subject = $"[Hoàn cọc thành công #{refund.Id}] HĐ #{refund.Agreement.AgreementNo}",

                Payload = System.Text.Json.JsonSerializer.Serialize(new

                {

                    refundId = refund.Id,

                    agreementId = refund.AgreementId,

                    agreementNo = refund.Agreement.AgreementNo,

                    amount = refund.Amount,

                    transactionReference = request.TransactionReference,

                    payoutMethod = "bank_transfer",

                    message = $"Quản lý cơ sở {refund.Agreement.Facility?.Name} đã chuyển khoản hoàn tất số tiền cọc {refund.Amount:N0} VND vào {bankTarget}. Mã giao dịch: {request.TransactionReference}."

                }),

                Status = "pending",

                ScheduledAt = now,

                CreatedAt = now

            });

        }



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerProcessRefundPayoutResultDto

        {

            RefundId = refund.Id,

            AgreementId = refund.AgreementId,

            Amount = refund.Amount,

            Status = refund.Status,

            PayoutMethod = refund.Provider,

            TransactionReference = request.TransactionReference ?? string.Empty,

            ProofImageUrl = request.ProofImageUrl,

            ProcessedByUserId = currentUserId,

            ProcessedAt = now,

            Message = "Đã xác nhận hoàn tất chuyển khoản tiền cọc cho khách hàng."

        };

    }
}
