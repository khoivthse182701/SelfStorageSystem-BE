using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Contracts.Common;
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
        var config = await dbContext.PricingConfigs.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var categories = await dbContext.StorageCategories.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            pricePerSquareMeter = config?.PricePerSquareMeter ?? 100m,
            updatedAt = config?.UpdatedAt,
            categories = categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                surchargeAmount = c.SurchargeAmount,
                isActive = c.IsActive
            })
        }, "Pricing config retrieved successfully."));
    }

    [HttpPut("api/v1/admin/pricing-config")]
    [Authorize(Roles = "Admin,Manager")]
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
    [Authorize(Roles = "Admin,Manager")]
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
    [Authorize(Roles = "Admin,Manager")]
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
}
