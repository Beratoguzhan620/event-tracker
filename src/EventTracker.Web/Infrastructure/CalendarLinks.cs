using EventTracker.Application.Calendar;
using EventTracker.Application.Common;
using EventTracker.Application.Events;

namespace EventTracker.Web.Infrastructure;

/// <summary>Builds the "add to calendar" file and link for an event.</summary>
public static class CalendarLinks
{
    public static IcsEvent ToIcs(EventDetails item, EventClock clock, string host, string? url) => new(
        Uid: $"event-{item.Id}@{host}",
        Title: item.Title,
        Description: url is null ? item.Description : $"{item.Description}\n\n{url}",
        Location: item.Location,
        StartsAtUtc: clock.ToUtc(item.StartsAt),
        EndsAtUtc: clock.ToUtc(item.EndsAt),
        StampUtc: clock.ToUtc(item.UpdatedAt),
        Url: url);

    public static string Google(EventDetails item, EventClock clock, string? url)
    {
        var dates = $"{IcsUtc(clock.ToUtc(item.StartsAt))}/{IcsUtc(clock.ToUtc(item.EndsAt))}";
        var details = url is null ? item.Description : $"{item.Description}\n\n{url}";
        return "https://calendar.google.com/calendar/render?action=TEMPLATE" +
               $"&text={Uri.EscapeDataString(item.Title)}" +
               $"&dates={dates}" +
               $"&details={Uri.EscapeDataString(details)}" +
               $"&location={Uri.EscapeDataString(item.Location)}";
    }

    public static string FileName(EventDetails item)
    {
        var slug = EventTracker.Data.SearchFolding.Fold(item.Title);
        slug = new string(slug.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return $"{(slug.Length == 0 ? "etkinlik" : slug[..Math.Min(slug.Length, 60)].Trim('-'))}.ics";
    }

    private static string IcsUtc(DateTime value) => value.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
