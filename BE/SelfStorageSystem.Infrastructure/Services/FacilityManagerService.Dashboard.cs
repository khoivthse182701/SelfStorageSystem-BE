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

    // 5. Dashboard Cơ Sở

    // ==========================================



    public async Task<ManagerFacilityDashboardDto> GetFacilityDashboardAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var facility = await _dbContext.Facilities

            .AsNoTracking()

            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken);

        if (facility == null)

        {

            throw AppException.FromError(ManagerErrors.FacilityNotFound);

        }



        // Units and occupancy

        var units = await _dbContext.StorageUnits

            .AsNoTracking()

            .Where(u => u.FacilityId == facilityId)

            .ToListAsync(cancellationToken);



        var totalUnits = units.Count;

        var occupied = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Occupied);

        var reserved = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Reserved);

        var available = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Available);

        var maintenance = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Maintenance || 

                                           u.PhysicalStatus == StorageUnitStatusConstants.OutOfService || 

                                           u.PhysicalStatus == StorageUnitStatusConstants.PendingInspection);



        var occupancyRate = totalUnits > 0

            ? Math.Round((double)(occupied + reserved) / totalUnits * 100.0, 1)

            : 0.0;



        // Daily schedule

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var todayCheckIns = await _dbContext.Reservations

            .CountAsync(r => r.FacilityId == facilityId && 

                             (r.Status == ReservationStatusConstants.Confirmed || r.Status == ReservationStatusConstants.Pending), cancellationToken);



        var todayCheckOuts = await _dbContext.MoveOutRequests

            .CountAsync(m => m.Agreement.FacilityId == facilityId && 

                             (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.InspectionPending || m.Status == MoveOutStatusConstants.Scheduled || m.RequestedMoveOutDate == today), cancellationToken);



        var todayUtcDate = DateTime.UtcNow.Date;

        var todayStart = new DateTimeOffset(todayUtcDate, TimeSpan.Zero);

        var todayEnd = todayStart.AddDays(1);



        var todayInspections = await _dbContext.Inspections

            .CountAsync(i => i.FacilityId == facilityId && 

                             i.InspectedAt >= todayStart && 

                             i.InspectedAt < todayEnd, cancellationToken);



        // Support Tickets

        var openTickets = await _dbContext.SupportTickets

            .CountAsync(t => t.FacilityId == facilityId && t.Status != TicketStatusConstants.Resolved && t.Status != TicketStatusConstants.Closed, cancellationToken);



        var urgentTickets = await _dbContext.SupportTickets

            .CountAsync(t => t.FacilityId == facilityId && t.Priority == TicketPriorityConstants.Urgent && t.Status != TicketStatusConstants.Resolved && t.Status != TicketStatusConstants.Closed, cancellationToken);



        // Monthly revenue & Outstanding debt

        var now = DateTimeOffset.UtcNow;

        var firstDayThisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);



        var thisMonthRevenue = await _dbContext.Payments

            .AsNoTracking()

            .Where(p => p.TargetInvoice != null && 

                        ((p.TargetInvoice.Agreement != null && p.TargetInvoice.Agreement.FacilityId == facilityId) || (p.TargetInvoice.Reservation != null && p.TargetInvoice.Reservation.FacilityId == facilityId)) &&

                        p.Status == PaymentConstants.StatusSucceeded &&

                        (p.PaidAt >= firstDayThisMonth || (p.PaidAt == null && p.CreatedAt >= firstDayThisMonth)))

            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;



        var totalOutstandingDebt = await _dbContext.Invoices

            .AsNoTracking()

            .Where(i => ((i.Agreement != null && i.Agreement.FacilityId == facilityId) || (i.Reservation != null && i.Reservation.FacilityId == facilityId)) &&

                        i.Status != InvoiceStatusConstants.Paid &&

                        i.Status != InvoiceStatusConstants.Voided)

            .SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), cancellationToken) ?? 0m;



        return new ManagerFacilityDashboardDto

        {

            FacilityId = facility.Id,

            FacilityCode = facility.Code,

            FacilityName = facility.Name,

            TotalUnits = totalUnits,

            OccupiedUnits = occupied,

            ReservedUnits = reserved,

            AvailableUnits = available,

            MaintenanceUnits = maintenance,

            OccupancyRate = occupancyRate,

            DailySchedule = new ManagerDailyScheduleDto

            {

                TodayCheckInsCount = todayCheckIns,

                TodayCheckOutsCount = todayCheckOuts,

                TodayInspectionsCount = todayInspections

            },

            OpenTicketsCount = openTickets,

            UrgentTicketsCount = urgentTickets,

            ThisMonthRevenue = thisMonthRevenue,

            TotalOutstandingDebt = totalOutstandingDebt

        };

    }



    // ==========================================

    // HELPERS

    // ==========================================



    private async Task EnsureFacilityAccessAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken)

    {

        var facilityExists = await _dbContext.Facilities

            .AnyAsync(f => f.Id == facilityId, cancellationToken);



        if (!facilityExists)

        {

            throw AppException.FromError(ManagerErrors.FacilityNotFound);

        }



        if (isSystemAdmin)

        {

            return;

        }



        var now = DateTimeOffset.UtcNow;

        var hasAccess = await _dbContext.StaffFacilityAssignments

            .AnyAsync(sfa => sfa.EmployeeId == currentUserId &&

                             sfa.FacilityId == facilityId &&

                             (sfa.AssignmentRole == StaffAssignmentRoles.FacilityManager ||

                              sfa.AssignmentRole == RoleConstants.FacilityManager ||

                              sfa.AssignmentRole.ToLower() == "manager" ||

                              sfa.AssignmentRole.ToLower() == "facility_manager") &&

                             (sfa.EndsAt == null || sfa.EndsAt > now),

                      cancellationToken);



        if (!hasAccess)

        {

            throw AppException.FromError(ManagerErrors.NotAssignedToFacility);

        }

    }



    private async Task<long> GetManagerEmployeeIdAsync(

        long currentUserId,

        CancellationToken cancellationToken)

    {

        var employee = await _dbContext.EmployeeProfiles

            .FirstOrDefaultAsync(ep => ep.UserId == currentUserId, cancellationToken);



        if (employee != null)

        {

            return employee.UserId;

        }



        // Check if user exists in Users table

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);

        if (user == null)

        {

            throw AppException.FromError(ManagerErrors.ManagerEmployeeProfileNotFound);

        }



        // Auto-create employee profile for administrator performing manager duties if needed

        var now = DateTimeOffset.UtcNow;

        var newEmployee = new EmployeeProfile

        {

            UserId = user.Id,

            EmployeeCode = $"{AdminConstants.DefaultEmployeeCodePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % AdminConstants.AutoEmployeeCodeModulo:D6}",

            FullName = user.Email,

            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),

            EmploymentStatus = EmploymentStatusConstants.Active,

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.EmployeeProfiles.Add(newEmployee);

        await _dbContext.SaveChangesAsync(cancellationToken);



        return newEmployee.UserId;

    }



    public async Task<List<ManagerFacilityScopeDto>> GetMyFacilitiesAsync(

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        if (isSystemAdmin)

        {

            return await _dbContext.Facilities

                .AsNoTracking()

                .OrderBy(f => f.Name)

                .Select(f => new ManagerFacilityScopeDto

                {

                    Id = f.Id,

                    Code = f.Code,

                    Name = f.Name,

                    AddressLine = f.AddressLine,

                    City = f.City,

                    Status = f.Status,

                    AssignmentRole = RoleConstants.SystemAdministrator

                })

                .ToListAsync(cancellationToken);

        }



        var now = DateTimeOffset.UtcNow;

        var assigned = await _dbContext.StaffFacilityAssignments

            .AsNoTracking()

            .Include(sfa => sfa.Facility)

            .Where(sfa => sfa.EmployeeId == currentUserId &&

                          (sfa.AssignmentRole == StaffAssignmentRoles.FacilityManager ||

                           sfa.AssignmentRole == RoleConstants.FacilityManager ||

                           sfa.AssignmentRole.ToLower() == "manager" ||

                           sfa.AssignmentRole.ToLower() == "facility_manager") &&

                          (sfa.EndsAt == null || sfa.EndsAt > now))

            .OrderBy(sfa => sfa.Facility.Name)

            .Select(sfa => new ManagerFacilityScopeDto

            {

                Id = sfa.Facility.Id,

                Code = sfa.Facility.Code,

                Name = sfa.Facility.Name,

                AddressLine = sfa.Facility.AddressLine,

                City = sfa.Facility.City,

                Status = sfa.Facility.Status,

                AssignmentRole = StaffAssignmentRoles.FacilityManager

            })

            .ToListAsync(cancellationToken);



        return assigned;

    }
}
