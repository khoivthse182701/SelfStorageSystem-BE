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

    [Fact]
    public async Task DeclareStoredItems_AgreementNotActive_ThrowsAppValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 200,
            AgreementNo = "AGR-200",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 20,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = "terminated",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerStoredItemService(db);
        var request = new DeclareStoredItemsRequest(new List<CreateStoredItemDto>
        {
            new CreateStoredItemDto("Hộp tài liệu", StoredItemCategoryConstants.Documents, null, 1, 500000m, null, null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.DeclareStoredItemsAsync(customerId: 7, agreementId: 200, request));
    }

    [Fact]
    public async Task DeclareStoredItems_InvalidCategory_ThrowsAppValidationException()
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
            new CreateStoredItemDto("Bàn làm việc", "invalid_category_123", null, 1, 500000m, null, null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.DeclareStoredItemsAsync(customerId: 7, agreementId: 100, request));
    }

    [Fact]
    public async Task DeclareStoredItems_InvalidRiskClassification_ThrowsAppValidationException()
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
            new CreateStoredItemDto("Tivi", StoredItemCategoryConstants.Electronics, null, 1, 500000m, "super_extreme_risk", null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.DeclareStoredItemsAsync(customerId: 7, agreementId: 100, request));
    }

    [Fact]
    public async Task DeclareStoredItems_NegativeEstimatedValue_ThrowsAppValidationException()
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
            new CreateStoredItemDto("Tivi", StoredItemCategoryConstants.Electronics, null, 1, -500000m, ItemRiskClassificationConstants.Standard, null)
        });

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() => service.DeclareStoredItemsAsync(customerId: 7, agreementId: 100, request));
    }

    [Fact]
    public async Task GetDeclaredStoredItems_WithSearchAndCategory_FiltersCorrectly()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 300,
            AgreementNo = "AGR-300",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 30,
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

        db.StoredItems.AddRange(
            new StoredItem
            {
                Id = 1,
                AgreementId = 300,
                ItemName = "Bàn làm việc gỗ",
                Category = StoredItemCategoryConstants.Furniture,
                LocationNote = "Góc trái dưới cùng",
                Quantity = 1,
                EstimatedValue = 2000000m,
                DeclaredAt = DateTimeOffset.UtcNow
            },
            new StoredItem
            {
                Id = 2,
                AgreementId = 300,
                ItemName = "Tủ lạnh mini",
                Category = StoredItemCategoryConstants.Electronics,
                LocationNote = "Kệ trên bên phải",
                Quantity = 1,
                EstimatedValue = 3000000m,
                DeclaredAt = DateTimeOffset.UtcNow
            },
            new StoredItem
            {
                Id = 3,
                AgreementId = 300,
                ItemName = "Thùng sách tài liệu",
                Category = StoredItemCategoryConstants.Documents,
                LocationNote = "Góc trái sát cửa",
                Quantity = 3,
                EstimatedValue = 500000m,
                DeclaredAt = DateTimeOffset.UtcNow
            }
        );

        await db.SaveChangesAsync();
        var service = new CustomerStoredItemService(db);

        // Act 1: Search by location note keyword ("góc trái")
        var searchResult = await service.GetDeclaredStoredItemsAsync(customerId: 7, agreementId: 300, search: "góc trái");
        Assert.Equal(2, searchResult.Items.Count);
        Assert.All(searchResult.Items, item => Assert.Contains("Góc trái", item.LocationNote));

        // Act 2: Filter by category ("electronics")
        var categoryResult = await service.GetDeclaredStoredItemsAsync(customerId: 7, agreementId: 300, category: StoredItemCategoryConstants.Electronics);
        Assert.Single(categoryResult.Items);
        Assert.Equal("Tủ lạnh mini", categoryResult.Items[0].ItemName);

        // Act 3: Search by item name ("tủ lạnh") and category
        var combinedResult = await service.GetDeclaredStoredItemsAsync(customerId: 7, agreementId: 300, search: "tủ lạnh", category: StoredItemCategoryConstants.Electronics);
        Assert.Single(combinedResult.Items);
        Assert.Equal(1, combinedResult.TotalItemsCount);
    }

    [Fact]
    public async Task DeclareAndUpdateStoredItem_WithLocationNote_PersistsCorrectly()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        db.RentalAgreements.Add(new RentalAgreement
        {
            Id = 400,
            AgreementNo = "AGR-400",
            CustomerId = 7,
            FacilityId = 1,
            ReservationId = 40,
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
        var declareRequest = new DeclareStoredItemsRequest(new List<CreateStoredItemDto>
        {
            new CreateStoredItemDto("Quần áo mùa đông", StoredItemCategoryConstants.Clothing, "Đóng trong thùng nhựa", 2, 1000000m, null, null, "Góc trái dưới cùng")
        });

        // Act 1: Declare item with location note
        var declared = await service.DeclareStoredItemsAsync(customerId: 7, agreementId: 400, declareRequest);
        Assert.Single(declared.Items);
        Assert.Equal("Góc trái dưới cùng", declared.Items[0].LocationNote);

        var itemId = declared.Items[0].Id;

        // Act 2: Update item with new location note
        var updateRequest = new UpdateStoredItemRequest(
            ItemName: "Quần áo mùa đông",
            Category: StoredItemCategoryConstants.Clothing,
            Description: "Đóng trong thùng nhựa",
            Quantity: 2,
            EstimatedValue: 1000000m,
            RiskClassification: ItemRiskClassificationConstants.Standard,
            PhotoUrl: null,
            LocationNote: "Chuyển lên kệ tầng 2"
        );

        var updated = await service.UpdateStoredItemAsync(customerId: 7, agreementId: 400, itemId: itemId, updateRequest);
        Assert.Equal("Chuyển lên kệ tầng 2", updated.LocationNote);
    }
}

