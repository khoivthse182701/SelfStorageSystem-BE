using System.Text.RegularExpressions;

namespace SelfStorageSystem.Infrastructure.Common;

public static class StorageCategoryClassifier
{
    private static readonly string[] ValidKeywords =
    [
        "kho", "tủ", "tu", "lạnh", "lanh", "đông", "dong", "mát", "mat",
        "khô", "kho", "hóa chất", "hoa chat", "sưởi", "suoi", "nhiệt độ", "nhiet do",
        "tài liệu", "tai lieu", "hàng", "hang", "storage", "cold", "dry", "locker",
        "climate", "warm", "chemical", "archive", "freeze", "seafood"
    ];

    public static bool IsValidCategoryName(string? rawName, IEnumerable<string>? existingAdminCategories = null)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        var trimmed = rawName.Trim();
        if (trimmed.Length < 3)
            return false;

        // Block digits-only or special-chars-only
        if (Regex.IsMatch(trimmed, @"^\d+$") || !Regex.IsMatch(trimmed, @"[\p{L}]"))
            return false;

        // Block repeated single character spam (e.g., "aaaaa")
        if (trimmed.Distinct().Count() <= 2)
            return false;

        var lower = trimmed.ToLowerInvariant();
        if (existingAdminCategories != null &&
            existingAdminCategories.Any(c => string.Equals(c?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return ValidKeywords.Any(kw => lower.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    public static string ClassifyCanonicalGroup(string rawName)
    {
        var lower = rawName.Trim().ToLowerInvariant();
        if (lower.Contains("hóa chất") || lower.Contains("hoa chat") || lower.Contains("chemical"))
            return "Kho hóa chất";
        if (lower.Contains("lạnh") || lower.Contains("lanh") || lower.Contains("đông") || lower.Contains("dong") || lower.Contains("cold") || lower.Contains("freeze") || lower.Contains("seafood"))
            return "Kho lạnh / Thủy hải sản";
        if (lower.Contains("mát") || lower.Contains("mat") || lower.Contains("điều hòa") || lower.Contains("nhiệt độ") || lower.Contains("climate"))
            return "Kho mát điều hòa";
        if (lower.Contains("sưởi") || lower.Contains("suoi") || lower.Contains("warm"))
            return "Kho sưởi ấm";
        if (lower.Contains("khô") || lower.Contains("dry") || lower.Contains("tủ") || lower.Contains("locker"))
            return "Kho hàng khô";

        return rawName.Trim();
    }

    public static bool IsUnitTypeMatchingCategory(string? unitTypeCode, string? unitTypeName, string canonicalCategory)
    {
        var lowerCat = canonicalCategory.ToLowerInvariant();
        var code = (unitTypeCode ?? "").ToUpperInvariant();
        var name = (unitTypeName ?? "").ToLowerInvariant();

        if (lowerCat.Contains("hóa chất") || lowerCat.Contains("chemical"))
            return true;
        if (lowerCat.Contains("lạnh") || lowerCat.Contains("seafood") || lowerCat.Contains("thủy hải sản"))
            return code.Contains("SEAFOOD") || name.Contains("seafood") || name.Contains("cold");
        if (lowerCat.Contains("mát") || lowerCat.Contains("điều hòa") || lowerCat.Contains("climate"))
            return code.Contains("CLIMATE") || name.Contains("climate");
        if (lowerCat.Contains("sưởi") || lowerCat.Contains("warm"))
            return code.Contains("WARM") || name.Contains("warm") || name.Contains("heat");
        if (lowerCat.Contains("khô") || lowerCat.Contains("dry") || lowerCat.Contains("locker"))
            return code.Contains("DRY") || name.Contains("dry") || code.Contains("LOCKER") || name.Contains("locker");

        return true;
    }

    public static long FindBestMatchingUnitTypeId(IEnumerable<SelfStorageSystem.Domain.Entities.UnitType> unitTypes, string rawCategory, decimal areaM2)
    {
        var list = unitTypes.Where(t => t.IsActive).ToList();
        if (list.Count == 0) return 1;

        var canonical = ClassifyCanonicalGroup(rawCategory);
        var matching = list.Where(t => IsUnitTypeMatchingCategory(t.Code, t.Name, canonical)).ToList();
        var pool = matching.Count > 0 ? matching : list;

        return pool.OrderBy(t => Math.Abs((t.AreaM2 ?? (t.LengthM * t.WidthM)) - areaM2)).First().Id;
    }
}
