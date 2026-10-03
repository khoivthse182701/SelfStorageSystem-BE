using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerStoredItemService : ICustomerStoredItemService
{
    private readonly SelfStorageDbContext _dbContext;

    private static readonly string[] ProhibitedKeywords = new[]
    {
        "explosive", "flammable", "weapon", "gun", "drug", "illegal", "chemical", "poison",
        "chất nổ", "chất dễ cháy", "vũ khí", "ma túy", "hàng cấm", "hóa chất độc hại", "súng"
    };

    public CustomerStoredItemService(SelfStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RentalStoredItemsResponseDto> DeclareStoredItemsAsync(
        long customerId,
        long agreementId,
        DeclareStoredItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(StoredItemErrors.AgreementNotFound);

        if (request.Items == null || !request.Items.Any())
            throw AppException.FromError(StoredItemErrors.EmptyItemsList);

        var itemsToAdd = new List<StoredItem>();
        var now = DateTimeOffset.UtcNow;

        foreach (var itemDto in request.Items)
        {
            if (string.IsNullOrWhiteSpace(itemDto.ItemName))
                throw AppException.FromError(StoredItemErrors.EmptyItemName);

            if (itemDto.Quantity <= 0)
                throw AppException.FromError(StoredItemErrors.InvalidQuantity);

            CheckProhibitedContent(itemDto.ItemName, itemDto.Description);

            var category = string.IsNullOrWhiteSpace(itemDto.Category) 
                ? StoredItemCategoryConstants.Other 
                : itemDto.Category.ToLowerInvariant();

            var riskClassification = string.IsNullOrWhiteSpace(itemDto.RiskClassification)
                ? ItemRiskClassificationConstants.Standard
                : itemDto.RiskClassification.ToLowerInvariant();

            itemsToAdd.Add(new StoredItem
            {
                AgreementId = agreementId,
                ItemName = itemDto.ItemName.Trim(),
                Category = category,
                Description = itemDto.Description?.Trim(),
                Quantity = itemDto.Quantity,
                EstimatedValue = itemDto.EstimatedValue,
                RiskClassification = riskClassification,
                PhotoUrl = itemDto.PhotoUrl?.Trim(),
                DeclaredAt = now,
                UpdatedAt = now
            });
        }

        _dbContext.StoredItems.AddRange(itemsToAdd);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetDeclaredStoredItemsAsync(customerId, agreementId, cancellationToken);
    }

    public async Task<RentalStoredItemsResponseDto> GetDeclaredStoredItemsAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(StoredItemErrors.AgreementNotFound);

        var items = await _dbContext.StoredItems
            .AsNoTracking()
            .Where(i => i.AgreementId == agreementId)
            .OrderByDescending(i => i.DeclaredAt)
            .ToListAsync(cancellationToken);

        var itemDtos = items.Select(i => new StoredItemDto(
            Id: i.Id,
            AgreementId: i.AgreementId,
            ItemName: i.ItemName,
            Category: i.Category,
            Description: i.Description,
            Quantity: i.Quantity,
            EstimatedValue: i.EstimatedValue,
            RiskClassification: i.RiskClassification,
            PhotoUrl: i.PhotoUrl,
            DeclaredAt: i.DeclaredAt,
            UpdatedAt: i.UpdatedAt
        )).ToList();

        var totalCount = itemDtos.Sum(i => i.Quantity);
        var totalValue = itemDtos.Sum(i => (i.EstimatedValue ?? 0m) * i.Quantity);

        return new RentalStoredItemsResponseDto(
            AgreementId: agreementId,
            TotalItemsCount: totalCount,
            TotalEstimatedValue: totalValue,
            Items: itemDtos
        );
    }

    private static void CheckProhibitedContent(string name, string? description)
    {
        var combinedText = $"{name} {description}".ToLowerInvariant();
        foreach (var keyword in ProhibitedKeywords)
        {
            if (combinedText.Contains(keyword))
            {
                throw AppException.FromError(StoredItemErrors.ProhibitedItem);
            }
        }
    }
}
