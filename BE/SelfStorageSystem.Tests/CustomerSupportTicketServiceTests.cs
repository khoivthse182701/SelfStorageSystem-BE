using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfStorageSystem.Contracts.Customer.SupportTickets;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerSupportTicketServiceTests
{
    private SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    [Fact]
    public async Task CreateTicket_Success_ReturnsSummaryDto()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var service = new CustomerSupportTicketService(db);
        var request = new CreateSupportTicketRequest(
            FacilityId: 1,
            AgreementId: null,
            StorageUnitId: null,
            Category: TicketCategoryConstants.Access,
            Priority: TicketPriorityConstants.High,
            Subject: "Locked Out",
            Description: "Cannot open door PIN failed",
            Attachments: new List<CreateTicketAttachmentDto>
            {
                new CreateTicketAttachmentDto("photo.jpg", "image/jpeg", 1024, "https://storage.example.com/photo.jpg", null)
            }
        );

        // Act
        var result = await service.CreateTicketAsync(customerId: 100, request);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("TCK-", result.TicketNo);
        Assert.Equal(1, result.FacilityId);
        Assert.Equal("FAC-01", result.FacilityName);
        Assert.Equal(TicketCategoryConstants.Access, result.Category);
        Assert.Equal(TicketPriorityConstants.High, result.Priority);
        Assert.Equal("Locked Out", result.Subject);
        Assert.Equal(TicketStatusConstants.Open, result.Status);
        Assert.Equal(TicketDisplayStatusConstants.Reported, result.DisplayStatus);
    }

    [Fact]
    public async Task CreateTicket_FacilityNotFound_ThrowsAppException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new CustomerSupportTicketService(db);
        var request = new CreateSupportTicketRequest(
            FacilityId: 999,
            AgreementId: null,
            StorageUnitId: null,
            Category: TicketCategoryConstants.Access,
            Priority: TicketPriorityConstants.Normal,
            Subject: "Test",
            Description: "Test Desc",
            Attachments: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<AppNotFoundException>(() => service.CreateTicketAsync(customerId: 100, request));
    }

    [Fact]
    public async Task GetCustomerTickets_ReturnsTicketsForCustomer()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        db.SupportTickets.Add(new SupportTicket
        {
            Id = 10,
            TicketNo = "TCK-20260101-0001",
            CustomerId = 100,
            FacilityId = 1,
            Category = TicketCategoryConstants.Access,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Issue 1",
            Description = "Desc 1",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.SupportTickets.Add(new SupportTicket
        {
            Id = 11,
            TicketNo = "TCK-20260101-0002",
            CustomerId = 200, // Different customer
            FacilityId = 1,
            Category = TicketCategoryConstants.Other,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Issue 2",
            Description = "Desc 2",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new CustomerSupportTicketService(db);

        // Act
        var result = await service.GetCustomerTicketsAsync(customerId: 100);

        // Assert
        Assert.Single(result);
        Assert.Equal(10, result[0].Id);
        Assert.Equal("FAC-01", result[0].FacilityName);
    }

    [Fact]
    public async Task AddMessage_AppendsMessageAndUpdatesStatus()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.SupportTickets.Add(new SupportTicket
        {
            Id = 1,
            TicketNo = "TCK-001",
            CustomerId = 100,
            FacilityId = 1,
            Category = TicketCategoryConstants.Access,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Test",
            Description = "Initial",
            Status = TicketStatusConstants.WaitingForCustomer,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerSupportTicketService(db);
        var request = new AddTicketMessageRequest("Here is additional info", null);

        // Act
        var result = await service.AddMessageAsync(customerId: 100, ticketId: 1, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Here is additional info", result.Body);

        var updatedTicket = await db.SupportTickets.FindAsync(1L);
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketStatusConstants.InProgress, updatedTicket.Status);
    }

    [Fact]
    public async Task ConfirmAndRateTicket_Success_ClosesTicketAndSavesRating()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.SupportTickets.Add(new SupportTicket
        {
            Id = 1,
            TicketNo = "TCK-001",
            CustomerId = 100,
            FacilityId = 1,
            Category = TicketCategoryConstants.Access,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Test",
            Description = "Initial",
            Status = TicketStatusConstants.Resolved,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerSupportTicketService(db);
        var request = new ConfirmAndRateTicketRequest(Score: 5, Comment: "Great service!");

        // Act
        await service.ConfirmAndRateTicketAsync(customerId: 100, ticketId: 1, request);

        // Assert
        var updatedTicket = await db.SupportTickets.Include(t => t.ServiceRating).FirstOrDefaultAsync(t => t.Id == 1);
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketStatusConstants.Closed, updatedTicket.Status);
        Assert.NotNull(updatedTicket.ServiceRating);
        Assert.Equal(5, updatedTicket.ServiceRating.Score);
        Assert.Equal("Great service!", updatedTicket.ServiceRating.Comment);
    }

    [Fact]
    public async Task ConfirmAndRateTicket_NotResolved_ThrowsAppException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.SupportTickets.Add(new SupportTicket
        {
            Id = 1,
            TicketNo = "TCK-001",
            CustomerId = 100,
            FacilityId = 1,
            Category = TicketCategoryConstants.Access,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Test",
            Description = "Initial",
            Status = TicketStatusConstants.InProgress, // Not resolved
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerSupportTicketService(db);
        var request = new ConfirmAndRateTicketRequest(Score: 4, Comment: null);

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.ConfirmAndRateTicketAsync(customerId: 100, ticketId: 1, request));
    }
}
