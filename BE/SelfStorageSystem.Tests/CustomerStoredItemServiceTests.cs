using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerStoredItemServiceTests
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
    public async Task DeclareStoredItems_Success_SavesItemsAndCalculatesTotals()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 100,
            AgreementNo = "AGR-001",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 10,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerStoredItemService(db);
        var request = new DeclareStoredItemsRequest(new List<CreateStoredItemDto>
        {
            new CreateStoredItemDto("Sofa Gỗ", StoredItemCategoryConstants.Furniture, "Bộ sofa phòng khách", 1, 15000000m, ItemRiskClassificationConstants.Standard, null),
            new CreateStoredItemDto("Tivi Sony 55 inch", StoredItemCategoryConstants.Electronics, "Tivi đóng thùng gỗ", 2, 10000000m, ItemRiskClassificationConstants.Fragile, null)
        });

        // Act
        var result = await service.DeclareStoredItemsAsync(customerId: 7, agreementId: 100, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.AgreementId);
        Assert.Equal(3, result.TotalItemsCount); // 1 sofa + 2 tivi
        Assert.Equal(35000000m, result.TotalEstimatedValue); // 15m + 2*10m
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task DeclareStoredItems_AgreementNotFound_ThrowsAppNotFoundException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new CustomerStoredItemService(db);
        var request = new DeclareStoredItemsRequest(new List<CreateStoredItemDto>
        {
            new CreateStoredItemDto("Hộp tài liệu", StoredItemCategoryConstants.Documents, null, 1, 500000m, null, null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppNotFoundException>(() => service.DeclareStoredItemsAsync(customerId: 999, agreementId: 999, request));
    }

    [Fact]
    public async Task DeclareStoredItems_ProhibitedItem_ThrowsAppValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 100,
            AgreementNo = "AGR-001",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 10,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerStoredItemService(db);
        var request = new DeclareStoredItemsRequest(new List<CreateStoredItemDto>
        {
            new CreateStoredItemDto("Pháo hoa và bình pháo cấm", StoredItemCategoryConstants.Other, "Chất nổ dễ cháy", 1, 100000m, null, null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.DeclareStoredItemsAsync(customerId: 7, agreementId: 100, request));
    }

    [Fact]
    public async Task GetDeclaredStoredItems_Success_ReturnsItems()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 100,
            AgreementNo = "AGR-001",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 10,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.StoredItems.Add(new StoredItem
        {
            Id = 1,
            AgreementId = 100,
            ItemName = "Tài liệu kế toán",
            Category = StoredItemCategoryConstants.Documents,
            Quantity = 5,
            EstimatedValue = 1000000m,
            RiskClassification = ItemRiskClassificationConstants.Standard,
            DeclaredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new CustomerStoredItemService(db);

        // Act
        var result = await service.GetDeclaredStoredItemsAsync(customerId: 7, agreementId: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.AgreementId);
        Assert.Single(result.Items);
        Assert.Equal("Tài liệu kế toán", result.Items[0].ItemName);
        Assert.Equal(5, result.TotalItemsCount);
    }
}
