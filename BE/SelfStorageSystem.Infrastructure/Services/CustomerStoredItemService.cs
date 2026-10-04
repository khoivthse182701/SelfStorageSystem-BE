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
        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(StoredItemErrors.AgreementNotFound);

        if (!string.Equals(agreement.Status, RentalAgreementStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StoredItemErrors.AgreementNotActive);

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

            if (itemDto.EstimatedValue.HasValue && itemDto.EstimatedValue < 0)
                throw AppException.FromError(StoredItemErrors.InvalidEstimatedValue);

            CheckProhibitedContent(itemDto.ItemName, itemDto.Description);

            var category = string.IsNullOrWhiteSpace(itemDto.Category) 
                ? StoredItemCategoryConstants.Other 
                : itemDto.Category.ToLowerInvariant();

            if (!StoredItemCategoryConstants.All.Contains(category))
                throw AppException.FromError(StoredItemErrors.InvalidCategory);

            var riskClassification = string.IsNullOrWhiteSpace(itemDto.RiskClassification)
                ? ItemRiskClassificationConstants.Standard
                : itemDto.RiskClassification.ToLowerInvariant();

            if (!ItemRiskClassificationConstants.All.Contains(riskClassification))
                throw AppException.FromError(StoredItemErrors.InvalidRiskClassification);

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

    public async Task<StoredItemDto> UpdateStoredItemAsync(
        long customerId,
        long agreementId,
        long itemId,
        UpdateStoredItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(StoredItemErrors.AgreementNotFound);

        if (!string.Equals(agreement.Status, RentalAgreementStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StoredItemErrors.AgreementNotActive);

        var item = await _dbContext.StoredItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AgreementId == agreementId, cancellationToken);

        if (item == null)
            throw AppException.FromError(StoredItemErrors.ItemNotFound);

        if (string.IsNullOrWhiteSpace(request.ItemName))
            throw AppException.FromError(StoredItemErrors.EmptyItemName);

        if (request.Quantity <= 0)
            throw AppException.FromError(StoredItemErrors.InvalidQuantity);

        if (request.EstimatedValue.HasValue && request.EstimatedValue < 0)
            throw AppException.FromError(StoredItemErrors.InvalidEstimatedValue);

        CheckProhibitedContent(request.ItemName, request.Description);

        var category = string.IsNullOrWhiteSpace(request.Category)
            ? StoredItemCategoryConstants.Other
            : request.Category.ToLowerInvariant();

        if (!StoredItemCategoryConstants.All.Contains(category))
            throw AppException.FromError(StoredItemErrors.InvalidCategory);

        var riskClassification = string.IsNullOrWhiteSpace(request.RiskClassification)
            ? ItemRiskClassificationConstants.Standard
            : request.RiskClassification.ToLowerInvariant();

        if (!ItemRiskClassificationConstants.All.Contains(riskClassification))
            throw AppException.FromError(StoredItemErrors.InvalidRiskClassification);

        item.ItemName = request.ItemName.Trim();
        item.Category = category;
        item.Description = request.Description?.Trim();
        item.Quantity = request.Quantity;
        item.EstimatedValue = request.EstimatedValue;
        item.RiskClassification = riskClassification;
        item.PhotoUrl = request.PhotoUrl?.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StoredItemDto(
            Id: item.Id,
            AgreementId: item.AgreementId,
            ItemName: item.ItemName,
            Category: item.Category,
            Description: item.Description,
            Quantity: item.Quantity,
            EstimatedValue: item.EstimatedValue,
            RiskClassification: item.RiskClassification,
            PhotoUrl: item.PhotoUrl,
            DeclaredAt: item.DeclaredAt,
            UpdatedAt: item.UpdatedAt
        );
    }

    public async Task DeleteStoredItemAsync(
        long customerId,
        long agreementId,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(StoredItemErrors.AgreementNotFound);

        if (!string.Equals(agreement.Status, RentalAgreementStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StoredItemErrors.AgreementNotActive);

        var item = await _dbContext.StoredItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AgreementId == agreementId, cancellationToken);

        if (item == null)
            throw AppException.FromError(StoredItemErrors.ItemNotFound);

        _dbContext.StoredItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
