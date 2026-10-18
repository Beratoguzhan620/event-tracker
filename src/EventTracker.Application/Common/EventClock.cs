namespace EventTracker.Application.Common;

/// <summary>
/// The current time in the time zone events are planned in. Event times are stored as local wall-clock
/// values, so "now" has to be compared in the same zone.
/// </summary>
public sealed class EventClock(TimeProvider timeProvider, TimeZoneInfo zone)
{
    public TimeZoneInfo Zone { get; } = zone;

    public DateTime Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime;

    public DateTime Today => Now.Date;

    /// <summary>Converts a stored local event time to UTC (for calendar files).</summary>
    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    public static TimeZoneInfo FindZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;

        // Turkey has used a fixed UTC+3 offset since 2016; keep working where tz data is missing.
        return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
    }
}
