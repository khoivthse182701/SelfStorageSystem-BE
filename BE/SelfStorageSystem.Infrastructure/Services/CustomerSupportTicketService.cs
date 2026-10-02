using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Customer.SupportTickets;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerSupportTicketService : ICustomerSupportTicketService
{
    private readonly SelfStorageDbContext _dbContext;

    public CustomerSupportTicketService(SelfStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupportTicketSummaryDto> CreateTicketAsync(long customerId, CreateSupportTicketRequest request, CancellationToken cancellationToken = default)
    {
        var facilityExists = await _dbContext.Facilities.AnyAsync(f => f.Id == request.FacilityId, cancellationToken);
        if (!facilityExists)
            throw AppException.FromError(TicketErrors.FacilityNotFound);

        if (request.AgreementId.HasValue)
        {
            var agreementValid = await _dbContext.RentalAgreements
                .AnyAsync(a => a.Id == request.AgreementId && a.CustomerId == customerId && a.FacilityId == request.FacilityId, cancellationToken);
            if (!agreementValid)
                throw AppException.FromError(TicketErrors.AgreementNotFound);
        }

        if (request.StorageUnitId.HasValue)
        {
            var unitValid = await _dbContext.StorageUnits
                .AnyAsync(u => u.Id == request.StorageUnitId && u.FacilityId == request.FacilityId, cancellationToken);
            if (!unitValid)
                throw AppException.FromError(TicketErrors.StorageUnitNotFound);
        }

        var ticketNo = $"TCK-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var ticket = new SupportTicket
        {
            TicketNo = ticketNo,
            CustomerId = customerId,
            FacilityId = request.FacilityId,
            AgreementId = request.AgreementId,
            StorageUnitId = request.StorageUnitId,
            Category = request.Category,
            Priority = request.Priority ?? TicketPriorityConstants.Normal,
            Subject = request.Subject,
            Description = request.Description,
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (request.Attachments != null && request.Attachments.Any())
        {
            var initialMessage = new TicketMessage
            {
                AuthorUserId = customerId,
                Body = request.Description,
                IsInternal = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            foreach (var att in request.Attachments)
            {
                initialMessage.TicketAttachmentMessages.Add(new TicketAttachment
                {
                    TicketId = ticket.Id,
                    UploadedBy = customerId,
                    FileName = att.FileName,
                    MimeType = att.MimeType,
                    FileSizeBytes = att.FileSizeBytes,
                    ObjectUrl = att.ObjectUrl,
                    Sha256 = att.Sha256,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            ticket.TicketMessages.Add(initialMessage);
        }

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var facilityName = await _dbContext.Facilities.Where(f => f.Id == ticket.FacilityId).Select(f => f.Code).FirstOrDefaultAsync(cancellationToken);

        return new SupportTicketSummaryDto(
            Id: ticket.Id,
            TicketNo: ticket.TicketNo,
            FacilityId: ticket.FacilityId,
            FacilityName: facilityName ?? "Unknown",
            Category: ticket.Category,
            Priority: ticket.Priority,
            Subject: ticket.Subject,
            Status: ticket.Status,
            DisplayStatus: GetDisplayStatus(ticket.Status),
            CreatedAt: ticket.CreatedAt,
            ResolvedAt: ticket.ResolvedAt,
            RatingScore: null
        );
    }

    public async Task<IReadOnlyList<SupportTicketSummaryDto>> GetCustomerTicketsAsync(long customerId, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.ServiceRating)
            .Where(t => t.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

        return tickets.Select(t => new SupportTicketSummaryDto(
            Id: t.Id,
            TicketNo: t.TicketNo,
            FacilityId: t.FacilityId,
            FacilityName: t.Facility.Code,
            Category: t.Category,
            Priority: t.Priority,
            Subject: t.Subject,
            Status: t.Status,
            DisplayStatus: GetDisplayStatus(t.Status),
            CreatedAt: t.CreatedAt,
            ResolvedAt: t.ResolvedAt,
            RatingScore: t.ServiceRating?.Score
        )).ToList();
    }

    public async Task<SupportTicketDetailDto> GetTicketDetailAsync(long customerId, long ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.SupportTickets
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.StorageUnit)
            .Include(t => t.ServiceRating)
            .Include(t => t.TicketAttachments.Where(a => a.MessageId == null))
            .Include(t => t.TicketMessages.Where(m => !m.IsInternal))
                .ThenInclude(m => m.AuthorUser)
                    .ThenInclude(u => u.CustomerProfile)
            .Include(t => t.TicketMessages.Where(m => !m.IsInternal))
                .ThenInclude(m => m.AuthorUser)
                    .ThenInclude(u => u.EmployeeProfile)
            .Include(t => t.TicketMessages.Where(m => !m.IsInternal))
                .ThenInclude(m => m.TicketAttachmentMessages)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CustomerId == customerId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        var messages = ticket.TicketMessages.OrderBy(m => m.CreatedAt).Select(m => new TicketMessageDto(
            Id: m.Id,
            TicketId: m.TicketId,
            AuthorUserId: m.AuthorUserId,
            AuthorName: m.AuthorUser?.CustomerProfile?.FullName ?? m.AuthorUser?.EmployeeProfile?.FullName,
            AuthorRole: m.AuthorUser?.CustomerProfile != null ? "Customer" : m.AuthorUser?.EmployeeProfile != null ? "Staff" : null,
            Body: m.Body,
            CreatedAt: m.CreatedAt,
            Attachments: m.TicketAttachmentMessages.Select(a => new TicketAttachmentDto(
                Id: a.Id,
                TicketId: a.TicketId,
                MessageId: a.MessageId,
                UploadedBy: a.UploadedBy,
                FileName: a.FileName,
                MimeType: a.MimeType,
                FileSizeBytes: a.FileSizeBytes,
                ObjectUrl: a.ObjectUrl,
                CreatedAt: a.CreatedAt
            )).ToList()
        )).ToList();

        var initialAttachments = ticket.TicketAttachments.Where(a => a.MessageId == null).Select(a => new TicketAttachmentDto(
            Id: a.Id,
            TicketId: a.TicketId,
            MessageId: null,
            UploadedBy: a.UploadedBy,
            FileName: a.FileName,
            MimeType: a.MimeType,
            FileSizeBytes: a.FileSizeBytes,
            ObjectUrl: a.ObjectUrl,
            CreatedAt: a.CreatedAt
        )).ToList();

        ServiceRatingDto? ratingDto = null;
        if (ticket.ServiceRating != null)
        {
            ratingDto = new ServiceRatingDto(
                ticket.ServiceRating.TicketId,
                ticket.ServiceRating.CustomerId,
                ticket.ServiceRating.Score,
                ticket.ServiceRating.Comment,
                ticket.ServiceRating.CreatedAt
            );
        }

        return new SupportTicketDetailDto(
            Id: ticket.Id,
            TicketNo: ticket.TicketNo,
            FacilityId: ticket.FacilityId,
            FacilityName: ticket.Facility.Code,
            AgreementId: ticket.AgreementId,
            StorageUnitId: ticket.StorageUnitId,
            UnitName: ticket.StorageUnit?.UnitCode,
            Category: ticket.Category,
            Priority: ticket.Priority,
            Subject: ticket.Subject,
            Description: ticket.Description,
            Status: ticket.Status,
            DisplayStatus: GetDisplayStatus(ticket.Status),
            Resolution: ticket.Resolution,
            ResolvedAt: ticket.ResolvedAt,
            CreatedAt: ticket.CreatedAt,
            UpdatedAt: ticket.UpdatedAt,
            InitialAttachments: initialAttachments,
            Messages: messages,
            Rating: ratingDto
        );
    }

    public async Task<TicketMessageDto> AddMessageAsync(long customerId, long ticketId, AddTicketMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw AppException.FromError(TicketErrors.EmptyMessage);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CustomerId == customerId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        if (ticket.Status == TicketStatusConstants.Closed || ticket.Status == TicketStatusConstants.Cancelled)
            throw AppException.FromError(TicketErrors.TicketClosed);

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = customerId,
            Body = request.Body,
            IsInternal = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (request.Attachments != null && request.Attachments.Any())
        {
            foreach (var att in request.Attachments)
            {
                message.TicketAttachmentMessages.Add(new TicketAttachment
                {
                    TicketId = ticket.Id,
                    UploadedBy = customerId,
                    FileName = att.FileName,
                    MimeType = att.MimeType,
                    FileSizeBytes = att.FileSizeBytes,
                    ObjectUrl = att.ObjectUrl,
                    Sha256 = att.Sha256,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        _dbContext.TicketMessages.Add(message);
        
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (ticket.Status == TicketStatusConstants.WaitingForCustomer)
        {
            ticket.Status = TicketStatusConstants.InProgress;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        var userProfile = await _dbContext.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == customerId, cancellationToken);

        return new TicketMessageDto(
            Id: message.Id,
            TicketId: message.TicketId,
            AuthorUserId: message.AuthorUserId,
            AuthorName: userProfile?.FullName,
            AuthorRole: "Customer",
            Body: message.Body,
            CreatedAt: message.CreatedAt,
            Attachments: message.TicketAttachmentMessages.Select(a => new TicketAttachmentDto(
                Id: a.Id,
                TicketId: a.TicketId,
                MessageId: a.MessageId,
                UploadedBy: a.UploadedBy,
                FileName: a.FileName,
                MimeType: a.MimeType,
                FileSizeBytes: a.FileSizeBytes,
                ObjectUrl: a.ObjectUrl,
                CreatedAt: a.CreatedAt
            )).ToList()
        );
    }

    public async Task ConfirmAndRateTicketAsync(long customerId, long ticketId, ConfirmAndRateTicketRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Score < 1 || request.Score > 5)
            throw AppException.FromError(TicketErrors.InvalidRatingScore);

        var ticket = await _dbContext.SupportTickets
            .Include(t => t.ServiceRating)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CustomerId == customerId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        if (ticket.Status != TicketStatusConstants.Resolved)
            throw AppException.FromError(TicketErrors.TicketNotResolved);

        if (ticket.ServiceRating != null)
            throw AppException.FromError(TicketErrors.TicketAlreadyRated);

        ticket.Status = TicketStatusConstants.Closed;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (!ticket.ResolvedAt.HasValue)
        {
             ticket.ResolvedAt = DateTimeOffset.UtcNow;
        }

        var rating = new ServiceRating
        {
            TicketId = ticket.Id,
            CustomerId = customerId,
            Score = request.Score,
            Comment = request.Comment,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.ServiceRatings.Add(rating);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetDisplayStatus(string status) => status switch
    {
        TicketStatusConstants.Open => TicketDisplayStatusConstants.Reported,
        TicketStatusConstants.InProgress => TicketDisplayStatusConstants.Investigating,
        TicketStatusConstants.WaitingForCustomer => TicketDisplayStatusConstants.Investigating,
        TicketStatusConstants.WaitingForMaintenance => TicketDisplayStatusConstants.Investigating,
        TicketStatusConstants.Resolved => TicketDisplayStatusConstants.Resolved,
        TicketStatusConstants.Closed => TicketDisplayStatusConstants.Closed,
        TicketStatusConstants.Cancelled => TicketDisplayStatusConstants.Cancelled,
        _ => status
    };
}
