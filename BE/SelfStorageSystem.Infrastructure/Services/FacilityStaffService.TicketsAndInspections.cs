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
    // ==========================================
    // 🛠️ NHÓM 4: HỖ TRỢ TICKET VỀ LOGISTICS (Dọn dẹp, Sửa chữa, Kiểm tra)
    // ==========================================

    public async Task<IReadOnlyList<StaffLogisticsTicketDto>> GetLogisticsTicketsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? category = null,
        string? status = null,
        bool? onlyAssignedToMe = null,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);

        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Facility)
            .Include(t => t.Customer)
            .Include(t => t.StorageUnit)
            .Include(t => t.Agreement)
            .Include(t => t.TicketAssignment).ThenInclude(ta => ta!.Employee)
            .Include(t => t.TicketChargeProposals)
            .Where(t => t.FacilityId == targetFacilityId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            if (cat.Contains("damage"))
            {
                query = query.Where(t => t.Category.ToLower().Contains("damage") || t.Category.ToLower() == "damage");
            }
            else
            {
                query = query.Where(t => t.Category.ToLower() == cat);
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            if (st == "completed" || st == "resolved")
            {
                query = query.Where(t => t.Status.ToLower() == "completed" || t.Status.ToLower() == "resolved" || t.Status.ToLower() == "closed");
            }
            else
            {
                query = query.Where(t => t.Status.ToLower() == st);
            }
        }

        if (onlyAssignedToMe == true)
        {
            var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
            query = query.Where(t => t.TicketAssignment != null && t.TicketAssignment.EmployeeId == employee.UserId);
        }

        var list = await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync(cancellationToken);

        return list.Select(t => new StaffLogisticsTicketDto(
            Id: t.Id,
            TicketNo: t.TicketNo,
            FacilityId: t.FacilityId,
            FacilityName: t.Facility.Name,
            StorageUnitId: t.StorageUnitId,
            UnitCode: t.StorageUnit?.UnitCode,
            CustomerId: t.CustomerId,
            CustomerName: t.Customer.FullName,
            AgreementId: t.AgreementId,
            AgreementNo: t.Agreement?.AgreementNo,
            Category: t.Category,
            Priority: t.Priority,
            Subject: t.Subject,
            Description: t.Description,
            Status: t.Status,
            AssignedEmployeeId: t.TicketAssignment?.EmployeeId,
            AssignedEmployeeName: t.TicketAssignment?.Employee?.FullName,
            Resolution: t.Resolution,
            ResolvedAt: t.ResolvedAt,
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt,
            HasProposedCharges: t.TicketChargeProposals.Any()
        )).ToList();
    }

    public async Task AssignTicketToStaffAsync(
        long employeeUserId,
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .Include(t => t.TicketAssignment)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (ticket.TicketAssignment == null)
        {
            _dbContext.TicketAssignments.Add(new TicketAssignment
            {
                TicketId = ticket.Id,
                EmployeeId = employee.UserId,
                AssignedAt = now
            });
        }
        else
        {
            ticket.TicketAssignment.EmployeeId = employee.UserId;
            ticket.TicketAssignment.AssignedAt = now;
        }

        ticket.Status = TicketStatusConstants.InProgress;
        ticket.UpdatedAt = now;

        var existingStaffTask = await _dbContext.StaffTasks
            .FirstOrDefaultAsync(t => t.TicketId == ticket.Id, cancellationToken);
        if (existingStaffTask != null)
        {
            existingStaffTask.AssignedEmployeeId = employee.UserId;
            if (existingStaffTask.Status == "todo")
            {
                existingStaffTask.Status = "in_progress";
                existingStaffTask.ProgressPercent = 50;
            }
            existingStaffTask.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TicketChargeProposalDto> ProposeTicketChargeAsync(
        long employeeUserId,
        long ticketId,
        StaffProposeChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var proposal = new TicketChargeProposal
        {
            TicketId = ticket.Id,
            ProposedBy = employee.UserId,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? request.Reason
                : $"{request.Reason}: {request.Description}",
            Amount = request.Amount,
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.TicketChargeProposals.Add(proposal);

        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
        {
            _dbContext.TicketAttachments.Add(new TicketAttachment
            {
                TicketId = ticket.Id,
                UploadedBy = employee.UserId,
                FileName = "damage_photo.jpg",
                ObjectUrl = request.PhotoUrl,
                MimeType = "image/jpeg",
                FileSizeBytes = 1024,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TicketChargeProposalDto(
            Id: proposal.Id,
            TicketId: proposal.TicketId,
            ProposedBy: employee.UserId,
            ProposedByName: employee.FullName,
            Description: proposal.Description,
            Amount: proposal.Amount,
            Status: proposal.Status,
            CreatedAt: proposal.CreatedAt
        );
    }

    public async Task<StaffCompleteTicketResultDto> CompleteTicketAsync(
        long employeeUserId,
        long ticketId,
        StaffCompleteTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.SupportTickets
            .Include(t => t.StorageUnit)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        ticket.Status = TicketStatusConstants.Resolved;
        ticket.Resolution = request.Resolution ?? "Hoàn tất xử lý công việc bởi nhân viên.";
        ticket.ResolvedAt = now;
        ticket.UpdatedAt = now;

        long? unlockedUnitId = null;
        string? unlockedUnitCode = null;
        string? unlockedUnitStatus = null;

        // Quy tắc nghiệp vụ đặc biệt:
        // Nếu đây là ticket "Dọn vệ sinh/Sửa chữa ô kho", khi Staff bấm complete, hệ thống tự động mở khóa ô kho đó từ Maintenance/Cleaning về lại Available để bán tiếp!
        if (ticket.StorageUnitId.HasValue && ticket.StorageUnit != null)
        {
            var unit = ticket.StorageUnit;
            var isMaintenanceOrCleaning =
                ticket.Category.Contains("clean", StringComparison.OrdinalIgnoreCase) ||
                ticket.Category.Contains("maint", StringComparison.OrdinalIgnoreCase) ||
                ticket.Category.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Maintenance, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Cleaning, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.UnderMaintenanceValue, StringComparison.OrdinalIgnoreCase);

            if (isMaintenanceOrCleaning)
            {
                var oldStatus = unit.PhysicalStatus;

                var hasActiveRental = await _dbContext.UnitAllocations
                    .AnyAsync(ua => ua.StorageUnitId == unit.Id 
                                 && ua.AllocationKind == AllocationKindConstants.Rental 
                                 && ua.Status == AllocationStatusConstants.Active, cancellationToken);

                if (hasActiveRental)
                {
                    unit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                    unit.IsListed = false;
                }
                else
                {
                    unit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    unit.IsListed = true;
                }
                unit.UpdatedAt = now;

                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                {
                    StorageUnitId = unit.Id,
                    OldStatus = oldStatus,
                    NewStatus = unit.PhysicalStatus,
                    Reason = $"Hoàn tất ticket {ticket.TicketNo}: {ticket.Resolution}",
                    ChangedBy = employeeUserId,
                    ChangedAt = now
                });

                unlockedUnitId = unit.Id;
                unlockedUnitCode = unit.UnitCode;
                unlockedUnitStatus = unit.PhysicalStatus;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffCompleteTicketResultDto(
            TicketId: ticket.Id,
            TicketNo: ticket.TicketNo,
            Status: ticket.Status,
            Resolution: ticket.Resolution,
            ResolvedAt: ticket.ResolvedAt.Value,
            UnlockedUnitId: unlockedUnitId,
            UnlockedUnitCode: unlockedUnitCode,
            UnlockedUnitStatus: unlockedUnitStatus
        );
    }

    // ==========================================
    // MODULE 3: Staff submits ticket report (Resolved / Severe_Need_Relocation)
    // ==========================================
    public async Task<StaffTicketReportResultDto> SubmitTicketReportAsync(
        long employeeUserId,
        long ticketId,
        StaffSubmitTicketReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.SupportTickets
            .Include(t => t.StorageUnit)
            .Include(t => t.Customer)
            .Include(t => t.StaffTasks)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == employeeUserId, cancellationToken);
        if (employee == null)
            throw AppException.FromError(StaffErrors.EmployeeProfileNotFound);

        var now = DateTimeOffset.UtcNow;
        var isSevere = request.AssessmentLevel.Equals("severe_need_relocation", StringComparison.OrdinalIgnoreCase);

        var reportMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = employeeUserId,
            Body = $"[Staff Inspection Report - {employee.FullName}]\nĐánh giá: {(isSevere ? "HƯ HỎNG NẶNG - CẦN ĐỔI KHO" : "ĐÃ XỬ LÝ XONG (RESOLVED)")}\nChi tiết: {request.ReportNotes}",
            IsInternal = false,
            CreatedAt = now
        };
        _dbContext.TicketMessages.Add(reportMessage);

        if (request.EvidenceImageUrls != null && request.EvidenceImageUrls.Count > 0)
        {
            foreach (var url in request.EvidenceImageUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _dbContext.TicketAttachments.Add(new TicketAttachment
                    {
                        TicketId = ticket.Id,
                        ObjectUrl = url.Trim(),
                        FileName = Path.GetFileName(url) ?? "evidence.jpg",
                        MimeType = "image/jpeg",
                        FileSizeBytes = 1024,
                        UploadedBy = employeeUserId,
                        CreatedAt = now
                    });
                }
            }
        }

        if (isSevere)
        {
            ticket.Status = TicketStatusConstants.SevereNeedRelocation;
            var resText = $"[Báo cáo Staff {employee.FullName}] Sự cố kho nghiêm trọng: {request.ReportNotes}. Cần Facility Manager thực hiện đổi kho khẩn cấp cho khách hàng.";

            ticket.Resolution = resText.Length > 900 ? resText.Substring(0, 900) : resText;
            if (ticket.StorageUnit != null)
            {
                ticket.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.UnderMaintenanceValue;
                ticket.StorageUnit.Notes = $"[LOCKED_BY:{employeeUserId}:{employee.FullName}] Sự cố hư hỏng lúc Check-in: {request.ReportNotes}";
                ticket.StorageUnit.UpdatedAt = now;
            }

            // Tự động tạo yêu cầu chuyển kho khẩn cấp (unit_transfer_requests) đẩy lên cho Facility Manager
            if (ticket.AgreementId.HasValue && ticket.StorageUnitId.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var existingTransfer = await _dbContext.UnitTransferRequests
                    .FirstOrDefaultAsync(tr => tr.AgreementId == ticket.AgreementId.Value && tr.Status == "pending", cancellationToken);
                if (existingTransfer == null)
                {
                    var unitTypeId = ticket.StorageUnit?.UnitTypeId ?? 1;
                    var transferReq = new UnitTransferRequest
                    {
                        AgreementId = ticket.AgreementId.Value,
                        RequestedUnitTypeId = unitTypeId,
                        RequestedFacilityId = ticket.FacilityId,
                        FromUnitId = ticket.StorageUnitId.Value,
                        RequestedEffectiveDate = today,
                        MoveOutDate = today,
                        MoveInDate = today,
                        Reason = ($"[Khẩn cấp lúc Check-in] Sự cố kho hỏng nặng: {request.ReportNotes}").Length > 900 ? ($"[Khẩn cấp lúc Check-in] Sự cố kho hỏng nặng: {request.ReportNotes}").Substring(0, 900) : $"[Khẩn cấp lúc Check-in] Sự cố kho hỏng nặng: {request.ReportNotes}",
                        Status = "pending",
                        RequestedBy = employeeUserId,
                        CreatedAt = now
                    };
                    _dbContext.UnitTransferRequests.Add(transferReq);
                }
            }
        }
        else
        {
            ticket.Status = TicketStatusConstants.Resolved;
            ticket.Resolution = request.ReportNotes;
            ticket.ResolvedAt = now;
        }
        ticket.UpdatedAt = now;

        foreach (var task in ticket.StaffTasks.Where(st => st.AssignedEmployeeId == employeeUserId && st.Status != "done" && st.Status != "completed"))
        {
            task.Status = "done";
            task.ProgressPercent = 100;
            task.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            try
            {
                await _realtimeNotifier.SendTicketUpdateAsync(ticket.FacilityId, new
                {
                    ticketId = ticket.Id,
                    ticketNo = ticket.TicketNo,
                    status = ticket.Status,
                    assessmentLevel = isSevere ? "severe_need_relocation" : "resolved",
                    resolution = ticket.Resolution,
                    facilityId = ticket.FacilityId,
                    updatedAt = now
                });
            }
            catch
            {
                // Non-blocking realtime update
            }
        }

        return new StaffTicketReportResultDto
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status,
            AssessmentLevel = isSevere ? "severe_need_relocation" : "resolved",
            Resolution = ticket.Resolution ?? string.Empty,
            ResolvedAt = ticket.ResolvedAt,
            Message = isSevere
                ? "Báo cáo sự cố nghiêm trọng đã gửi tới Facility Manager để kích hoạt chuyển kho khẩn cấp."
                : "Sự cố đã được xử lý và đánh dấu hoàn thành."
        };
    }

    public async Task<StaffLogisticsTicketDto> CreateCheckInIncidentTicketAsync(
        long employeeUserId,
        StaffCreateCheckInIncidentTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedFacilityId = request.FacilityId > 0 ? request.FacilityId : (long?)null;
        long? targetFacilityId = requestedFacilityId.HasValue
            ? await EnsureStrictFacilityAccessAsync(employeeUserId, requestedFacilityId, cancellationToken)
            : null;
        var now = DateTimeOffset.UtcNow;

        long customerId = 0;
        string? resolvedCustomerName = null;
        string? resolvedAgreementNo = request.AgreementNo;
        StorageUnit? resolvedUnit = null;

        if (request.AgreementId.HasValue && request.AgreementId.Value > 0)
        {
            var agr = await _dbContext.RentalAgreements
                .AsNoTracking()
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == request.AgreementId.Value, cancellationToken)
                ?? throw AppException.FromError(Error.NotFound("Staff.AgreementNotFound", "Rental agreement was not found."));

            if (targetFacilityId.HasValue && agr.FacilityId != targetFacilityId.Value)
                throw AppException.FromError(StaffErrors.FacilityMismatch);

            targetFacilityId ??= await EnsureStrictFacilityAccessAsync(employeeUserId, agr.FacilityId, cancellationToken);
            customerId = agr.CustomerId;
            resolvedCustomerName = agr.Customer?.FullName;
            resolvedAgreementNo ??= agr.AgreementNo;
        }

        if (request.ReservationId.HasValue && request.ReservationId.Value > 0)
        {
            var rsv = await _dbContext.Reservations
                .AsNoTracking()
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value, cancellationToken)
                ?? throw AppException.FromError(StaffErrors.ReservationNotFound);

            if (targetFacilityId.HasValue && rsv.FacilityId != targetFacilityId.Value)
                throw AppException.FromError(StaffErrors.FacilityMismatch);

            targetFacilityId ??= await EnsureStrictFacilityAccessAsync(employeeUserId, rsv.FacilityId, cancellationToken);
            if (customerId > 0 && rsv.CustomerId != customerId)
                throw AppException.FromError(Error.Validation("Staff.CustomerMismatch", "Reservation does not belong to the same customer as the agreement."));

            customerId = rsv.CustomerId;
            resolvedCustomerName ??= rsv.Customer?.FullName;
        }

        if (request.StorageUnitId.HasValue && request.StorageUnitId.Value > 0)
        {
            resolvedUnit = await _dbContext.StorageUnits
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.StorageUnitId.Value, cancellationToken)
                ?? throw AppException.FromError(StaffErrors.UnitNotFound);

            if (targetFacilityId.HasValue && resolvedUnit.FacilityId != targetFacilityId.Value)
                throw AppException.FromError(StaffErrors.FacilityMismatch);

            targetFacilityId ??= await EnsureStrictFacilityAccessAsync(employeeUserId, resolvedUnit.FacilityId, cancellationToken);
        }

        targetFacilityId ??= await EnsureStrictFacilityAccessAsync(employeeUserId, null, cancellationToken);

        if (customerId <= 0 && resolvedUnit != null)
        {
            var activeAlloc = await _dbContext.UnitAllocations
                .AsNoTracking()
                .Include(ua => ua.Agreement).ThenInclude(a => a!.Customer)
                .Include(ua => ua.Reservation).ThenInclude(r => r!.Customer)
                .Where(ua => ua.StorageUnitId == resolvedUnit.Id)
                .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)
                .ThenByDescending(ua => ua.Id)
                .FirstOrDefaultAsync(cancellationToken);

            customerId = activeAlloc?.Agreement?.CustomerId ?? activeAlloc?.Reservation?.CustomerId ?? 0;
            resolvedCustomerName ??= activeAlloc?.Agreement?.Customer?.FullName ?? activeAlloc?.Reservation?.Customer?.FullName;
        }

        if (customerId <= 0)
        {
            throw AppException.FromError(Error.Validation("Staff.CustomerNotResolved", "Không xác định được khách hàng hợp lệ từ hợp đồng hoặc ô kho được chọn."));
        }

        var unitCode = resolvedUnit?.UnitCode ?? request.UnitCode;
        var facilityName = await _dbContext.Facilities
            .AsNoTracking()
            .Where(f => f.Id == targetFacilityId.Value)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Facility";

        var normalizedPriority = string.IsNullOrWhiteSpace(request.Priority)
            ? TicketPriorityConstants.Urgent
            : request.Priority.Trim().ToLowerInvariant();
        if (!TicketPriorityConstants.All.Contains(normalizedPriority))
        {
            throw AppException.FromError(Error.Validation("Staff.InvalidPriority", "Ticket priority must be low, normal, high, or urgent."));
        }

        var rawSubject = $"[Sự cố Check-in] Kho {unitCode ?? (request.StorageUnitId.HasValue ? $"#{request.StorageUnitId}" : "Chưa gán")}";
        var rawDescription = request.Description.Trim();

        var ticket = new SupportTicket
        {
            TicketNo = $"TCK-URG-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
            FacilityId = targetFacilityId.Value,
            CustomerId = customerId,
            AgreementId = request.AgreementId > 0 ? request.AgreementId : null,
            StorageUnitId = request.StorageUnitId > 0 ? request.StorageUnitId : null,
            Category = TicketCategoryConstants.Unit,
            Priority = normalizedPriority,
            Subject = rawSubject.Length <= 255 ? rawSubject : rawSubject[..255],
            Description = rawDescription.Length <= 255 ? rawDescription : rawDescription[..255],
            Status = TicketStatusConstants.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffLogisticsTicketDto(
            Id: ticket.Id,
            TicketNo: ticket.TicketNo,
            FacilityId: ticket.FacilityId,
            FacilityName: facilityName,
            StorageUnitId: ticket.StorageUnitId,
            UnitCode: unitCode,
            CustomerId: ticket.CustomerId,
            CustomerName: resolvedCustomerName ?? request.CustomerName ?? "Customer",
            AgreementId: ticket.AgreementId,
            AgreementNo: resolvedAgreementNo,
            Category: ticket.Category,
            Priority: ticket.Priority,
            Subject: ticket.Subject,
            Description: ticket.Description,
            Status: ticket.Status,
            AssignedEmployeeId: null,
            AssignedEmployeeName: null,
            Resolution: null,
            ResolvedAt: null,
            CreatedAt: ticket.CreatedAt,
            UpdatedAt: ticket.UpdatedAt,
            HasProposedCharges: false
        );
    }

    // ==========================================
    // MODULE 4: Staff submits inspection report & executes deposit settlement
    // ==========================================
        public async Task<IReadOnlyList<StaffAssignedInspectionDto>> GetAssignedInspectionsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? status = null,
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);

        var moveOutQuery = _dbContext.MoveOutRequests
            .AsNoTracking()
            .Include(m => m.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
            .Include(m => m.Agreement).ThenInclude(a => a.Facility)
            .Include(m => m.Agreement).ThenInclude(a => a.UnitAllocations).ThenInclude(ua => ua.StorageUnit)
            .Where(m => m.Agreement.FacilityId == targetFacilityId);

        if (date.HasValue)
        {
            moveOutQuery = moveOutQuery.Where(m => m.RequestedMoveOutDate == date.Value);
        }

        var moveOuts = await moveOutQuery.OrderByDescending(m => m.RequestedMoveOutDate).ToListAsync(cancellationToken);

        var inspectionQuery = _dbContext.Inspections
            .AsNoTracking()
            .Include(i => i.Facility)
            .Include(i => i.StorageUnit)
            .Include(i => i.Agreement).ThenInclude(a => a!.Customer).ThenInclude(c => c.User)
            .Include(i => i.Agreement).ThenInclude(a => a!.UnitAllocations).ThenInclude(ua => ua.StorageUnit)
            .Include(i => i.InspectionItems)
            .Where(i => i.FacilityId == targetFacilityId);

        var inspections = await inspectionQuery.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        var inspectionByAgreement = inspections.Where(i => i.AgreementId.HasValue).GroupBy(i => i.AgreementId!.Value).ToDictionary(g => g.Key, g => g.First());

        var result = new List<StaffAssignedInspectionDto>();

        foreach (var m in moveOuts)
        {
            inspectionByAgreement.TryGetValue(m.AgreementId, out var insp);

            var alloc = m.Agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                        ?? m.Agreement.UnitAllocations.FirstOrDefault();
            var unit = alloc?.StorageUnit;
            var deposit = m.Agreement.DepositBalance;
            var totalDamage = insp?.InspectionItems?.Sum(item => item.ChargeAmount) ?? 0m;
            var penalty = totalDamage > deposit ? totalDamage - deposit : 0m;
            var refund = totalDamage < deposit ? deposit - totalDamage : 0m;

            var itemStatus = insp != null 
                ? (insp.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || insp.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase) ? "completed" : "pending")
                : (m.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "pending");

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(itemStatus, status.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new StaffAssignedInspectionDto
            {
                Id = insp?.Id ?? m.Id,
                AgreementId = m.AgreementId,
                AgreementNo = m.Agreement.AgreementNo ?? string.Empty,
                FacilityId = m.Agreement.FacilityId,
                FacilityName = m.Agreement.Facility?.Name ?? string.Empty,
                StorageUnitId = unit?.Id ?? 0,
                UnitCode = unit?.UnitCode ?? string.Empty,
                CustomerId = m.Agreement.CustomerId,
                CustomerName = m.Agreement.Customer?.FullName ?? string.Empty,
                CustomerPhone = m.Agreement.Customer?.User?.PhoneNumber ?? string.Empty,
                DepositAmount = deposit,
                Status = itemStatus,
                InspectionType = insp?.InspectionType ?? InspectionTypeConstants.CheckOut,
                OverallCondition = insp?.OverallCondition ?? (itemStatus == "completed" ? OverallConditionConstants.Good : OverallConditionConstants.Acceptable),
                Summary = insp?.Summary,
                InspectedAt = insp?.InspectedAt,
                ScheduledAt = m.RequestedMoveOutDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                ExpirationDate = m.Agreement.EndDate.ToString("yyyy-MM-dd"),
                RequestedMoveOutDate = m.RequestedMoveOutDate.ToString("yyyy-MM-dd"),
                RefundAmount = refund,
                PenaltyAmount = penalty,
                HasDamage = totalDamage > 0,
                RefundMethod = "bank_transfer",
                CreatedAt = m.CreatedAt
            });
        }

        var addedAgreementIds = moveOuts.Select(m => (long?)m.AgreementId).ToHashSet();
        foreach (var insp in inspections.Where(i => !addedAgreementIds.Contains(i.AgreementId)))
        {
            var alloc = insp.Agreement?.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                        ?? insp.Agreement?.UnitAllocations.FirstOrDefault();
            var unit = insp.StorageUnit ?? alloc?.StorageUnit;
            var deposit = insp.Agreement?.DepositBalance ?? 0m;
            var totalDamage = insp.InspectionItems?.Sum(item => item.ChargeAmount) ?? 0m;
            var penalty = totalDamage > deposit ? totalDamage - deposit : 0m;
            var refund = totalDamage < deposit ? deposit - totalDamage : 0m;

            var itemStatus = insp.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || insp.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase) ? "completed" : "pending";
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(itemStatus, status.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new StaffAssignedInspectionDto
            {
                Id = insp.Id,
                AgreementId = insp.AgreementId ?? 0,
                AgreementNo = insp.Agreement?.AgreementNo ?? string.Empty,
                FacilityId = insp.FacilityId,
                FacilityName = insp.Facility?.Name ?? string.Empty,
                StorageUnitId = unit?.Id ?? (insp.StorageUnitId ?? 0),
                UnitCode = unit?.UnitCode ?? string.Empty,
                CustomerId = insp.Agreement?.CustomerId ?? 0,
                CustomerName = insp.Agreement?.Customer?.FullName ?? string.Empty,
                CustomerPhone = insp.Agreement?.Customer?.User?.PhoneNumber ?? string.Empty,
                DepositAmount = deposit,
                Status = itemStatus,
                InspectionType = insp.InspectionType,
                OverallCondition = insp.OverallCondition,
                Summary = insp.Summary,
                InspectedAt = insp.InspectedAt,
                ScheduledAt = insp.InspectedAt ?? insp.CreatedAt,
                ExpirationDate = insp.Agreement?.EndDate.ToString("yyyy-MM-dd"),
                RequestedMoveOutDate = null,
                RefundAmount = refund,
                PenaltyAmount = penalty,
                HasDamage = totalDamage > 0,
                RefundMethod = "bank_transfer",
                CreatedAt = insp.CreatedAt
            });
        }

        return result;
    }

    private static string NormalizeInspectionItemCondition(string? rawCondition, decimal chargeAmount)
    {
        var cond = (rawCondition ?? string.Empty).Trim().ToLowerInvariant();
        return cond switch
        {
            "good" => InspectionItemConditionConstants.Good,
            "acceptable" => InspectionItemConditionConstants.Acceptable,
            "missing" => InspectionItemConditionConstants.Missing,
            "not_applicable" => InspectionItemConditionConstants.NotApplicable,
            "damaged" or "broken" or "dirty" or "cleaning" or "damage" => InspectionItemConditionConstants.Damaged,
            _ => chargeAmount > 0 ? InspectionItemConditionConstants.Damaged : InspectionItemConditionConstants.Good
        };
    }

    public async Task<StaffInspectionResultDto> SubmitInspectionReportAsync(
        long employeeUserId,
        long inspectionId,
        StaffSubmitInspectionReportRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inspection = await _dbContext.Inspections
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.Customer)
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.UnitAllocations)
                    .ThenInclude(ua => ua.StorageUnit)
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.MoveOutRequest)
            .FirstOrDefaultAsync(i => i.Id == inspectionId, cancellationToken);

        if (inspection == null)
        {
            inspection = await _dbContext.Inspections
                .Include(i => i.InspectionItems)
                .Include(i => i.Agreement).ThenInclude(a => a!.Customer)
                .Include(i => i.Agreement).ThenInclude(a => a!.UnitAllocations).ThenInclude(ua => ua.StorageUnit)
                .Include(i => i.Agreement).ThenInclude(a => a!.MoveOutRequest)
                .FirstOrDefaultAsync(i => i.AgreementId == inspectionId || 
                    (i.Agreement != null && i.Agreement.MoveOutRequest != null && i.Agreement.MoveOutRequest.Id == inspectionId), cancellationToken);
        }

        if (inspection == null)
        {
            var moveOutReq = await _dbContext.MoveOutRequests
                .Include(m => m.Agreement).ThenInclude(a => a.Customer)
                .Include(m => m.Agreement).ThenInclude(a => a.UnitAllocations).ThenInclude(ua => ua.StorageUnit)
                .FirstOrDefaultAsync(m => m.Id == inspectionId, cancellationToken);

            if (moveOutReq != null)
            {
                var agr = moveOutReq.Agreement;
                var alloc = agr.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                    ?? agr.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
                var storageUnitId = alloc?.StorageUnitId ?? throw AppException.FromError(StaffErrors.UnitNotFound);

                await EnsureStrictFacilityAccessAsync(employeeUserId, agr.FacilityId, cancellationToken);

                inspection = new Inspection
                {
                    AgreementId = agr.Id,
                    FacilityId = agr.FacilityId,
                    StorageUnitId = storageUnitId,
                    InspectedBy = employeeUserId,
                    InspectionType = InspectionTypeConstants.CheckOut,
                    OverallCondition = OverallConditionConstants.Good,
                    Summary = "Tự động khởi tạo từ yêu cầu trả kho",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Agreement = agr
                };
                _dbContext.Inspections.Add(inspection);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (inspection == null)
            throw AppException.FromError(StaffErrors.InspectionNotFound);

        await EnsureStrictFacilityAccessAsync(employeeUserId, inspection.FacilityId, cancellationToken);

        var agreement = inspection.Agreement;
        if (agreement == null)
            throw AppException.FromError(StaffErrors.AgreementNotFound);

        var now = DateTimeOffset.UtcNow;
        var isTerminated = string.Equals(agreement.Status, RentalAgreementStatusConstants.Terminated, StringComparison.OrdinalIgnoreCase);
        var deposit = isTerminated ? 0m : Math.Max(0m, agreement.DepositBalance);
        var damage = Math.Max(0m, request.DamageCost);

        decimal refundAmount = 0m;
        decimal extraPenalty = 0m;
        long? penaltyInvoiceId = null;
        string nextUnitStatus = StorageUnitStatusConstants.Available;
        long? refundRecordId = null;
        string settlementSummary = string.Empty;

        if (request.DamageItems != null && request.DamageItems.Count > 0)
        {
            foreach (var item in request.DamageItems)
            {
                var photo = request.PhotoUrls?.FirstOrDefault();
                var itemCost = item.Cost > 0 ? item.Cost : item.ChargeAmount;
                _dbContext.InspectionItems.Add(new InspectionItem
                {
                    InspectionId = inspection.Id,
                    ItemName = string.IsNullOrWhiteSpace(item.ItemName) ? "Hạng mục hư hại" : item.ItemName.Trim(),
                    Condition = NormalizeInspectionItemCondition(item.Condition, itemCost),
                    Notes = item.Notes ?? request.Summary,
                    PhotoUrl = photo,
                    ChargeAmount = itemCost > 0 ? itemCost : (damage / Math.Max(1, request.DamageItems.Count))
                });
            }
        }
        else if (request.DamageItemNames != null && request.DamageItemNames.Count > 0)
        {
            foreach (var itemName in request.DamageItemNames)
            {
                var photo = request.PhotoUrls?.FirstOrDefault();
                _dbContext.InspectionItems.Add(new InspectionItem
                {
                    InspectionId = inspection.Id,
                    ItemName = itemName,
                    Condition = damage > 0 ? InspectionItemConditionConstants.Damaged : InspectionItemConditionConstants.Good,
                    Notes = request.Summary,
                    PhotoUrl = photo,
                    ChargeAmount = damage / Math.Max(1, request.DamageItemNames.Count)
                });
            }
        }

        var cond = (request.OverallCondition ?? string.Empty).Trim().ToLowerInvariant();
        inspection.OverallCondition = cond switch
        {
            "good" => OverallConditionConstants.Good,
            "acceptable" => OverallConditionConstants.Acceptable,
            "damaged" or "dirty" or "cleaning" => OverallConditionConstants.Damaged,
            "unsafe" => OverallConditionConstants.Unsafe,
            _ => damage > 0 ? OverallConditionConstants.Damaged : OverallConditionConstants.Good
        };
        inspection.Summary = request.Summary;
        inspection.Status = InspectionStatusConstants.Completed;
        inspection.InspectedAt = now;

        var empProfile = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == employeeUserId, cancellationToken);
        var staffName = empProfile?.FullName ?? $"Staff #{employeeUserId}";

        var activeAllocation = agreement.UnitAllocations
            .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)
            .ThenByDescending(ua => ua.Id)
            .FirstOrDefault();
        var unit = activeAllocation?.StorageUnit;

        // Check if items are cleaning/dirty or damaged
        bool isDirtyOnly = false;
        if (request.DamageItems != null && request.DamageItems.Count > 0)
        {
            isDirtyOnly = request.DamageItems.All(it => 
                string.Equals(it.Condition, "dirty", StringComparison.OrdinalIgnoreCase) ||
                it.ItemName.ToLowerInvariant().Contains("vệ sinh") ||
                it.ItemName.ToLowerInvariant().Contains("dọn dẹp"));
        }
        else if (string.Equals(request.OverallCondition, "dirty", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(request.OverallCondition, "cleaning", StringComparison.OrdinalIgnoreCase))
        {
            isDirtyOnly = true;
        }

        // Determine next physical status and task ownership lock note
        if (damage == 0m && !isDirtyOnly)
        {
            nextUnitStatus = StorageUnitStatusConstants.Available;
            if (unit != null)
            {
                unit.Notes = null;
            }
        }
        else if (isDirtyOnly)
        {
            nextUnitStatus = StorageUnitStatusConstants.Cleaning;
            if (unit != null)
            {
                var cleanNote = $"[LOCKED_BY:{employeeUserId}:{staffName}] Cần dọn vệ sinh sau trả kho: {request.Summary ?? "Dọn dẹp rác tồn"}";
                if (cleanNote.Length > 250) cleanNote = cleanNote.Substring(0, 250);
                unit.Notes = cleanNote;
            }
        }
        else
        {
            nextUnitStatus = StorageUnitStatusConstants.Maintenance;
            if (unit != null)
            {
                var maintNote = $"[LOCKED_BY:{employeeUserId}:{staffName}] Hư hại sau trả kho: {request.Summary ?? "Hư hỏng cần bảo trì"}";
                if (maintNote.Length > 250) maintNote = maintNote.Substring(0, 250);
                unit.Notes = maintNote;
            }
        }

        // Ensure valid PaymentId and invoice reference to satisfy trigger trg_refunds_guard
        var agreementPayments = await _dbContext.Payments
            .Include(p => p.TargetInvoice)
                .ThenInclude(inv => inv.InvoiceLines)
            .Where(p => p.Status == PaymentConstants.StatusSucceeded && 
                (p.TargetInvoice.AgreementId == agreement.Id || (agreement.ReservationId > 0 && p.TargetInvoice.ReservationId == agreement.ReservationId)))
            .ToListAsync(cancellationToken);

        var depositPayment = agreementPayments
            .Where(p => (agreement.ReservationId > 0 && p.TargetInvoice.ReservationId == agreement.ReservationId) ||
                        p.TargetInvoice.InvoiceLines.Any(l => l.LineType == InvoiceLineTypeConstants.Deposit))
            .OrderByDescending(p => p.Amount)
            .FirstOrDefault();

        var validPayment = (depositPayment != null && depositPayment.Amount >= (deposit - damage))
            ? depositPayment
            : (agreementPayments.OrderByDescending(p => p.Amount).FirstOrDefault() ?? depositPayment);

        long resolvedPaymentId = validPayment?.Id ?? 0;

        if (resolvedPaymentId == 0)
        {
            var existingInv = await _dbContext.Invoices
                .FirstOrDefaultAsync(inv => inv.AgreementId == agreement.Id && inv.BillingPeriod == $"{now:yyyy-MM}", cancellationToken);

            long invoiceId;
            if (existingInv != null)
            {
                invoiceId = existingInv.Id;
            }
            else
            {
                var depInvoice = new Invoice
                {
                    InvoiceNo = $"INV-DEP-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                    AgreementId = agreement.Id,
                    CustomerId = agreement.CustomerId,
                    BillingPeriod = $"DEP-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    IssueDate = DateOnly.FromDateTime(now.DateTime),
                    DueDate = DateOnly.FromDateTime(now.DateTime),
                    Currency = PaymentConstants.CurrencyVnd,
                    SubtotalAmount = deposit > 0 ? deposit : (agreement.MonthlyRateSnapshot > 0 ? agreement.MonthlyRateSnapshot : 0m),
                    TaxAmount = 0m,
                    TotalAmount = deposit > 0 ? deposit : (agreement.MonthlyRateSnapshot > 0 ? agreement.MonthlyRateSnapshot : 0m),
                    PaidAmount = deposit > 0 ? deposit : (agreement.MonthlyRateSnapshot > 0 ? agreement.MonthlyRateSnapshot : 0m),
                    Status = InvoiceStatusConstants.Paid,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Invoices.Add(depInvoice);
                await _dbContext.SaveChangesAsync(cancellationToken);
                invoiceId = depInvoice.Id;
            }

            var depPayment = new Payment
            {
                CustomerId = agreement.CustomerId,
                TargetInvoiceId = invoiceId,
                Amount = deposit > 0 ? deposit : (agreement.MonthlyRateSnapshot > 0 ? agreement.MonthlyRateSnapshot : 0m),
                Currency = PaymentConstants.CurrencyVnd,
                Method = PaymentConstants.MethodBankTransfer,
                Provider = "system_deposit_reconciliation",
                IdempotencyKey = $"PAY-DEP-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                Status = PaymentConstants.StatusSucceeded,
                Metadata = "{}",
                PaidAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Payments.Add(depPayment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            resolvedPaymentId = depPayment.Id;
        }

        // TH1: Không hư hại (DamageCost == 0) -> Hoàn 100% cọc còn lại (nếu hợp đồng chưa bị thanh lý)
        if (damage == 0m)
        {
            refundAmount = deposit;
            extraPenalty = 0m;

            if (refundAmount > 0m)
            {
                var refund = new Refund
                {
                    PaymentId = resolvedPaymentId,
                    AgreementId = agreement.Id,
                    Amount = refundAmount,
                    Currency = "VND",
                    Reason = $"Hoàn trả 100% tiền cọc cho hợp đồng #{agreement.AgreementNo}. Nghiệm thu hiện trạng tốt, không hư hại.",
                    Provider = "pending_choice",
                    IdempotencyKey = $"REFUND-AGR-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                    Status = "pending",
                    RequestedBy = employeeUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Refunds.Add(refund);
                await _dbContext.SaveChangesAsync(cancellationToken);
                refundRecordId = refund.Id;
                settlementSummary = $"Không có hư hại. Kích hoạt quy trình hoàn trả 100% tiền cọc ({refundAmount:N0} đ).";
            }
            else
            {
                refundRecordId = null;
                settlementSummary = isTerminated
                    ? "Hợp đồng đã bị thanh lý (đã mất/cấn trừ cọc). Nghiệm thu hiện trạng kho tốt, không hoàn cọc."
                    : "Không có hư hại. Số dư cọc còn lại 0 đ nên không phát sinh hoàn trả.";
            }
        }
        // TH2: 0 < DamageCost <= deposit -> Khấu trừ cọc
        else if (damage <= deposit)
        {
            refundAmount = deposit - damage;
            extraPenalty = 0m;
            agreement.DepositBalance = refundAmount;

            if (refundAmount > 0)
            {
                var refund = new Refund
                {
                    PaymentId = resolvedPaymentId,
                    AgreementId = agreement.Id,
                    Amount = refundAmount,
                    Currency = "VND",
                    Reason = $"Khấu trừ chi phí thiệt hại hư hại {damage:N0} đ từ tiền cọc {deposit:N0} đ. Số tiền cọc còn lại hoàn cho khách: {refundAmount:N0} đ.",
                    Provider = "pending_choice",
                    IdempotencyKey = $"REFUND-AGR-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                    Status = "pending",
                    RequestedBy = employeeUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Refunds.Add(refund);
                await _dbContext.SaveChangesAsync(cancellationToken);
                refundRecordId = refund.Id;
            }
            settlementSummary = $"Có hư hại {damage:N0} đ. Đã khấu trừ vào cọc {deposit:N0} đ. Số tiền hoàn lại: {refundAmount:N0} đ. Kho chuyển bảo trì.";
        }
        // TH3: DamageCost > deposit -> Thu hồi 100% cọc + Xuất hóa đơn phạt
        else
        {
            refundAmount = 0m;
            extraPenalty = damage - deposit;
            agreement.DepositBalance = Math.Min(0m, agreement.DepositBalance) - extraPenalty;

            var penaltyInvoice = new Invoice
            {
                InvoiceNo = $"INV-PEN-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                AgreementId = agreement.Id,
                CustomerId = agreement.CustomerId,
                BillingPeriod = $"PEN-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                IssueDate = DateOnly.FromDateTime(now.DateTime),
                DueDate = DateOnly.FromDateTime(now.AddDays(7).DateTime),
                Currency = PaymentConstants.CurrencyVnd,
                SubtotalAmount = extraPenalty,
                TaxAmount = 0m,
                TotalAmount = extraPenalty,
                PaidAmount = 0m,
                Status = InvoiceStatusConstants.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Invoices.Add(penaltyInvoice);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.InvoiceLines.Add(new InvoiceLine
            {
                InvoiceId = penaltyInvoice.Id,
                LineType = InvoiceLineTypeConstants.Damage,
                Description = $"Phạt hư hỏng kho vượt quá tiền cọc (Tổng thiệt hại {damage:N0} đ - Cọc thu hồi {deposit:N0} đ)",
                Quantity = 1,
                UnitPrice = extraPenalty,
                LineAmount = extraPenalty,
                Metadata = "{}"
            });
            await _dbContext.SaveChangesAsync(cancellationToken);

            penaltyInvoice.Status = InvoiceStatusConstants.Open;
            penaltyInvoice.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            penaltyInvoiceId = penaltyInvoice.Id;

            // When damage exceeds deposit, deposit is completely forfeited and refund is 0. 
            // Do not insert into core.refunds because CK__refunds__amount requires amount > 0.
            refundRecordId = null;
            settlementSummary = $"Thiệt hại {damage:N0} đ vượt quá cọc {deposit:N0} đ. Thu hồi 100% cọc và tạo hóa đơn phạt thu thêm {extraPenalty:N0} đ (#{penaltyInvoice.InvoiceNo}).";
        }

        if (unit != null)
        {
            unit.PhysicalStatus = nextUnitStatus;
            unit.UpdatedAt = now;
        }

        if (activeAllocation != null)
        {
            activeAllocation.Status = AllocationStatusConstants.Released;
            activeAllocation.EndedAt = now;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today > activeAllocation.AllocationStartDate)
            {
                activeAllocation.AllocationEndDate = today;
            }
            else if (activeAllocation.AllocationEndDate <= activeAllocation.AllocationStartDate)
            {
                activeAllocation.AllocationEndDate = activeAllocation.AllocationStartDate.AddDays(1);
            }
        }

        if (agreement.Status == RentalAgreementStatusConstants.MoveOutScheduled)
        {
            agreement.Status = "checkout_pending";
        }
        else if (agreement.Status == "checkout_pending" || agreement.Status == RentalAgreementStatusConstants.Terminated)
        {
            // Keep checkout_pending or terminated
        }
        else
        {
            agreement.Status = RentalAgreementStatusConstants.Terminated;
        }
        agreement.UpdatedAt = now;

        var moveOut = agreement.MoveOutRequest ?? await _dbContext.MoveOutRequests
            .FirstOrDefaultAsync(m => m.AgreementId == agreement.Id && m.Status != MoveOutStatusConstants.Completed && m.Status != MoveOutStatusConstants.Cancelled, cancellationToken)
            ?? await _dbContext.MoveOutRequests
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(m => m.AgreementId == agreement.Id, cancellationToken);

        if (moveOut != null)
        {
            moveOut.Status = MoveOutStatusConstants.Completed;
            moveOut.FinalizedAt = now;
            moveOut.UpdatedAt = now;
        }

        var inspectionTasks = await _dbContext.StaffTasks
            .Where(t => t.FacilityId == agreement.FacilityId &&
                        t.TaskType == "inspection" &&
                        t.Title.Contains(agreement.AgreementNo) &&
                        t.Status != "done" && t.Status != "completed")
            .ToListAsync(cancellationToken);

        foreach (var inspTask in inspectionTasks)
        {
            inspTask.Status = "done";
            inspTask.ProgressPercent = 100;
            inspTask.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new StaffInspectionResultDto
        {
            InspectionId = inspection.Id,
            AgreementId = agreement.Id,
            AgreementNo = agreement.AgreementNo,
            OriginalDeposit = deposit,
            DamageCost = damage,
            RefundAmount = refundAmount,
            ExtraPenalty = extraPenalty,
            PenaltyInvoiceId = penaltyInvoiceId,
            NextUnitStatus = nextUnitStatus,
            RefundId = refundRecordId,
            SettlementSummary = settlementSummary,
            CompletedAt = now
        };
    }

    public async Task<IReadOnlyList<StaffRefundQueueItemDto>> GetPendingRefundsQueueAsync(
        long employeeUserId,
        long? facilityId = null,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);

        var refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Customer)
                    .ThenInclude(c => c.User)
            .Where(r => r.Agreement != null && r.Agreement.FacilityId == targetFacilityId && r.Amount > 0 && r.Status != "cancelled")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return refunds.Select(r =>
        {
            string? bankName = null;
            string? accountNumber = null;
            string? accountHolder = null;
            var reasonStr = r.Reason ?? string.Empty;
            if (reasonStr.Contains("Ngân hàng:"))
            {
                var parts = reasonStr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    var trimmed = parts[i].Trim();
                    if (trimmed.StartsWith("Ngân hàng:")) bankName = trimmed.Replace("Ngân hàng:", "").Trim();
                    else if (trimmed.StartsWith("STK:")) accountNumber = trimmed.Replace("STK:", "").Trim();
                    else if (trimmed.StartsWith("Chủ TK:")) accountHolder = trimmed.Replace("Chủ TK:", "").Trim();
                }
            }

            return new StaffRefundQueueItemDto
            {
                RefundId = r.Id,
                AgreementId = r.AgreementId,
                AgreementNo = r.Agreement?.AgreementNo,
                CustomerId = r.Agreement?.CustomerId ?? 0,
                CustomerName = r.Agreement?.Customer.FullName ?? string.Empty,
                CustomerPhone = r.Agreement?.Customer?.User?.PhoneNumber ?? string.Empty,
                Amount = r.Amount,
                Currency = r.Currency,
                Status = r.Status,
                Provider = r.Provider,
                Reason = r.Reason ?? string.Empty,
                BankName = bankName,
                BankAccountNumber = accountNumber,
                BankAccountHolder = accountHolder,
                CreatedAt = r.CreatedAt,
                RefundedAt = r.RefundedAt
            };
        }).ToList();
    }

    public async Task<StaffRefundQueueItemDto> ConfirmRefundPaymentAsync(
        long employeeUserId,
        StaffConfirmRefundPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.Refunds
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Customer)
                    .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(r => r.Id == request.RefundId, cancellationToken);

        if (refund == null)
        {
            refund = await _dbContext.Refunds
                .Include(r => r.Agreement)
                    .ThenInclude(a => a!.Customer)
                        .ThenInclude(c => c.User)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(r => (r.Agreement != null && r.Agreement.MoveOutRequest != null && r.Agreement.MoveOutRequest.Id == request.RefundId) || r.AgreementId == request.RefundId, cancellationToken);
        }

        if (refund == null)
            throw AppException.FromError(StaffErrors.RefundNotFound);

        var now = DateTimeOffset.UtcNow;
        refund.Status = "completed";
        refund.RefundedAt = now;
        refund.Provider = request.PaymentMethod;
        refund.Reason += $" | [Đã thanh toán bởi NV #{employeeUserId}] Phương thức: {request.PaymentMethod}, Số tiền: {request.AmountPaid:N0} đ. Ghi chú: {request.Note}. Minh chứng bill: {request.ReceiptPhotoUrl}";
        refund.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffRefundQueueItemDto
        {
            RefundId = refund.Id,
            AgreementId = refund.AgreementId,
            AgreementNo = refund.Agreement?.AgreementNo,
            CustomerId = refund.Agreement?.CustomerId ?? 0,
            CustomerName = refund.Agreement?.Customer?.FullName ?? string.Empty,
            CustomerPhone = refund.Agreement?.Customer?.User?.PhoneNumber ?? string.Empty,
            Amount = refund.Amount,
            Currency = refund.Currency,
            Status = refund.Status,
            Provider = refund.Provider,
            Reason = refund.Reason ?? string.Empty,
            ReceiptPhotoUrl = request.ReceiptPhotoUrl,
            CreatedAt = refund.CreatedAt,
            RefundedAt = refund.RefundedAt
        };
    }

    public async Task<StaffCounterCashPayoutResultDto> ProcessCounterCashPayoutAsync(
        long employeeUserId,
        long refundId,
        StaffCounterCashPayoutRequest request,
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
        {
            refund = await _dbContext.Refunds
                .Include(r => r.Agreement)
                    .ThenInclude(a => a!.Customer)
                        .ThenInclude(c => c.User)
                .Include(r => r.Agreement)
                    .ThenInclude(a => a!.Facility)
                .Include(r => r.Agreement)
                    .ThenInclude(a => a!.MoveOutRequest)
                .Include(r => r.RefundApproval)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(r => (r.Agreement != null && r.Agreement.MoveOutRequest != null && r.Agreement.MoveOutRequest.Id == refundId) || r.AgreementId == refundId, cancellationToken);
        }

        if (refund == null)
            throw AppException.FromError(StaffErrors.RefundNotFound);

        if (refund.Agreement == null)
            throw AppException.FromError(StaffErrors.AgreementNotFound);

        await EnsureStrictFacilityAccessAsync(employeeUserId, refund.Agreement.FacilityId, cancellationToken);

        if (refund.Amount <= 0m)
            throw AppException.FromError(StaffErrors.RefundAmountInvalid);

        if (refund.Status == RefundStatusConstants.Completed)
            throw AppException.FromError(StaffErrors.RefundAlreadyCompleted);

        // Chặn không cho Staff chi tiền mặt nếu khách đã đăng ký chuyển khoản ngân hàng (do Manager phụ trách)
        if (refund.Status == RefundStatusConstants.PendingManagerPayout)
            throw AppException.FromError(StaffErrors.RefundAlreadyInBankTransferFlow);

        var now = DateTimeOffset.UtcNow;
        await using var tx = _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        refund.Status = RefundStatusConstants.Completed;
        refund.Provider = RefundPayoutMethodConstants.Cash;
        refund.ProviderRefundId = $"CASH-{refund.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        refund.RefundedAt = now;
        refund.UpdatedAt = now;

        var approvalReason = System.Text.Json.JsonSerializer.Serialize(new
        {
            payoutMethod = RefundPayoutMethodConstants.Cash,
            receiptReference = request.ReceiptReference?.Trim(),
            proofImageUrl = request.ProofImageUrl?.Trim(),
            note = request.Note?.Trim()
        });

        if (refund.RefundApproval == null)
        {
            refund.RefundApproval = new RefundApproval
            {
                RefundId = refund.Id,
                Decision = "completed",
                DecidedBy = employeeUserId,
                Reason = approvalReason,
                DecidedAt = now
            };
            _dbContext.RefundApprovals.Add(refund.RefundApproval);
        }
        else
        {
            refund.RefundApproval.Decision = "completed";
            refund.RefundApproval.DecidedBy = employeeUserId;
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

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return new StaffCounterCashPayoutResultDto
        {
            RefundId = refund.Id,
            AgreementId = refund.AgreementId,
            Amount = refund.Amount,
            Status = refund.Status,
            PayoutMethod = refund.Provider,
            ProofImageUrl = request.ProofImageUrl,
            ReceiptReference = request.ReceiptReference?.Trim(),
            ProcessedByUserId = employeeUserId,
            ProcessedAt = now,
            Message = "Đã chi trả tiền mặt tại quầy và tất toán hợp đồng thành công."
        };
    }
}
