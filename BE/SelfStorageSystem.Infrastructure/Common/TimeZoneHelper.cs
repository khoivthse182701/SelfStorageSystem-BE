using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Infrastructure.Common;

public static class TimeZoneHelper
{
    private static readonly string[] VietnamTimeZones =
    {
        "SE Asia Standard Time",
        "Asia/Ho_Chi_Minh",
        "Asia/Bangkok",
        "Asia/Saigon"
    };

    /// <summary>
    /// Resolves a TimeZoneInfo cross-platform supporting both Windows ("SE Asia Standard Time")
    /// and IANA ("Asia/Ho_Chi_Minh") identifiers. Guaranteed to never fall back to UTC for Vietnam facilities.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? tzId, string? fallbackTz = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(tzId)) candidates.Add(tzId.Trim());
        if (!string.IsNullOrWhiteSpace(fallbackTz)) candidates.Add(fallbackTz.Trim());
        candidates.AddRange(VietnamTimeZones);

        foreach (var id in candidates)
        {
            if (TryGetTimeZone(id, out var tz))
            {
                return tz;
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "SE Asia (UTC+7)", "SE Asia (UTC+7)");
    }

    private static bool TryGetTimeZone(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    return true;
                }
                catch { }
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
                    return true;
                }
                catch { }
            }

            timeZone = null!;
            return false;
        }
    }
}
