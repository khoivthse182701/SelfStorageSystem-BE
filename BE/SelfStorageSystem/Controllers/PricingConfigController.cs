using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Controllers;

[ApiController]
public sealed class PricingConfigController(SelfStorageDbContext dbContext) : ControllerBase
{
    public sealed record UpdatePricingConfigRequest(decimal PricePerSquareMeter);
    public sealed record UpsertStorageCategoryRequest(string Name, decimal SurchargeAmount, bool IsActive = true);

    [HttpGet("api/v1/pricing-config")]
    [HttpGet("api/v1/admin/pricing-config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPricingConfig(CancellationToken ct)
    {
        var config = await dbContext.PricingConfigs.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new PricingConfig { PricePerSquareMeter = 100m, UpdatedAt = DateTimeOffset.UtcNow };
            dbContext.PricingConfigs.Add(config);
            await dbContext.SaveChangesAsync(ct);
        }

        var categories = await dbContext.StorageCategories.OrderBy(x => x.Id).ToListAsync(ct);
        if (categories.Count == 0)
        {
            var defaultCategories = new List<StorageCategory>
            {
                new() { Name = "Kho hàng khô", SurchargeAmount = 0m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new() { Name = "Kho mát điều hòa", SurchargeAmount = 500m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new() { Name = "Kho lạnh / Thủy hải sản", SurchargeAmount = 1000m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new() { Name = "Kho hóa chất", SurchargeAmount = 2000m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new() { Name = "Kho sưởi ấm", SurchargeAmount = 800m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
                new() { Name = "Kho tài liệu / Hồ sơ", SurchargeAmount = 300m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
            };
            dbContext.StorageCategories.AddRange(defaultCategories);
            await dbContext.SaveChangesAsync(ct);
            categories = defaultCategories;
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            pricePerSquareMeter = config.PricePerSquareMeter,
            updatedAt = config.UpdatedAt,
            categories = categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                surchargeAmount = c.SurchargeAmount,
                isActive = c.IsActive
            })
        }, "Pricing config retrieved successfully."));
    }

    [HttpPut("api/v1/pricing-config")]
    [HttpPut("api/v1/admin/pricing-config")]
    [Authorize(Roles = RoleConstants.OperationsOrManager)]
    public async Task<IActionResult> UpdatePricingConfig([FromBody] UpdatePricingConfigRequest request, CancellationToken ct)
    {
        if (request.PricePerSquareMeter < 0)
            return BadRequest(ApiResponse.Fail("PricePerSquareMeter must be >= 0."));

        var config = await dbContext.PricingConfigs.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new PricingConfig
            {
                PricePerSquareMeter = request.PricePerSquareMeter,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.PricingConfigs.Add(config);
        }
        else
        {
            config.PricePerSquareMeter = request.PricePerSquareMeter;
            config.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            pricePerSquareMeter = config.PricePerSquareMeter,
            updatedAt = config.UpdatedAt
        }, "Pricing config updated successfully."));
    }

    [HttpPost("api/v1/admin/storage-categories")]
    [HttpPost("api/v1/storage-categories")]
    [Authorize(Roles = RoleConstants.OperationsOrManager)]
    public async Task<IActionResult> CreateCategory([FromBody] UpsertStorageCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse.Fail("Category name is required."));

        var exists = await dbContext.StorageCategories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            return Conflict(ApiResponse.Fail("Category name already exists."));

        var cat = new StorageCategory
        {
            Name = name,
            SurchargeAmount = request.SurchargeAmount,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.StorageCategories.Add(cat);
        await dbContext.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(cat, "Storage category created successfully."));
    }

    [HttpPut("api/v1/admin/storage-categories/{id:long}")]
    [HttpPut("api/v1/storage-categories/{id:long}")]
    [Authorize(Roles = RoleConstants.OperationsOrManager)]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpsertStorageCategoryRequest request, CancellationToken ct)
    {
        var cat = await dbContext.StorageCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cat == null)
            return NotFound(ApiResponse.Fail("Storage category not found."));

        if (!string.IsNullOrWhiteSpace(request.Name))
            cat.Name = request.Name.Trim();
        cat.SurchargeAmount = request.SurchargeAmount;
        cat.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(cat, "Storage category updated successfully."));
    }

    [HttpDelete("api/v1/admin/storage-categories/{id:long}")]
    [HttpDelete("api/v1/storage-categories/{id:long}")]
    [Authorize(Roles = RoleConstants.OperationsOrManager)]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct)
    {
        var cat = await dbContext.StorageCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cat == null)
            return NotFound(ApiResponse.Fail("Storage category not found."));

        dbContext.StorageCategories.Remove(cat);
        await dbContext.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Storage category deleted successfully."));
    }
}
