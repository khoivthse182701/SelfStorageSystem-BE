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
}
