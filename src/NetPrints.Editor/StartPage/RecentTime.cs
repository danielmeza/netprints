using System.Globalization;

namespace NetPrints.Editor.StartPage;

/// <summary>The date groups and relative dates of the recent list.</summary>
internal static class RecentTime
{
    /// <summary>The group of the pinned entries.</summary>
    public const string Pinned = "Pinned";

    /// <summary>The group of the entries opened on the current day.</summary>
    public const string Today = "Today";

    /// <summary>The group of the entries opened in the last six days before today.</summary>
    public const string ThisWeek = "This week";

    /// <summary>The group of the entries opened in the last 29 days before this week.</summary>
    public const string ThisMonth = "This month";

    /// <summary>The group of every older entry.</summary>
    public const string Older = "Older";

    /// <summary>Names the group an entry belongs to.</summary>
    /// <param name="pinned">Whether the entry is pinned; a pinned entry is in <see cref="Pinned"/> whatever its age.</param>
    /// <param name="opened">When the entry was last opened.</param>
    /// <param name="now">The current time.</param>
    /// <param name="zone">The time zone that decides where a day starts.</param>
    /// <returns>One of the group names of this class.</returns>
    public static string GroupOf(bool pinned, DateTimeOffset opened, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (pinned)
        {
            return Pinned;
        }

        return DaysAgo(opened, now, zone) switch
        {
            <= 0 => Today,
            < DaysInWeek => ThisWeek,
            < DaysInMonth => ThisMonth,
            _ => Older,
        };
    }

    /// <summary>Writes when an entry was opened as a person would say it.</summary>
    /// <param name="opened">When the entry was last opened.</param>
    /// <param name="now">The current time.</param>
    /// <param name="zone">The time zone that decides where a day starts.</param>
    /// <param name="culture">The culture of a date written in full.</param>
    /// <returns>"Just now", "5 minutes ago", "2 hours ago", "Yesterday", "3 days ago", or the date.</returns>
    public static string Describe(DateTimeOffset opened, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
    {
        TimeSpan age = now - opened;
        int days = DaysAgo(opened, now, zone);
        return age.TotalMinutes < 1 ? "Just now"
            : age.TotalHours < 1 ? Plural((int)age.TotalMinutes, "minute")
            : days <= 0 ? Plural((int)age.TotalHours, "hour")
            : days == 1 ? "Yesterday"
            : days < DaysInWeek ? Plural(days, "day")
            : TimeZoneInfo.ConvertTime(opened, zone).ToString("d MMM yyyy", culture);
    }

    private const int DaysInWeek = 7;
    private const int DaysInMonth = 30;

    private static int DaysAgo(DateTimeOffset opened, DateTimeOffset now, TimeZoneInfo zone) =>
        (TimeZoneInfo.ConvertTime(now, zone).Date - TimeZoneInfo.ConvertTime(opened, zone).Date).Days;

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";
}
