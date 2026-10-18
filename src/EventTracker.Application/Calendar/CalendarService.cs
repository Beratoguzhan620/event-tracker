using EventTracker.Application.Common;
using EventTracker.Data;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Calendar;

public enum CalendarView
{
    Month,
    Week,
}

public sealed record CalendarEvent(
    int Id,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    string Location,
    string CategoryIcon,
    bool IsFull);

public sealed record CalendarDay(DateOnly Date, bool InFocus, bool IsToday, List<CalendarEvent> Events);

public sealed record CalendarPage
{
    public CalendarView View { get; init; }

    /// <summary>First day of the shown month, or Monday of the shown week.</summary>
    public DateOnly Anchor { get; init; }

    public List<CalendarDay> Days { get; init; } = [];

    public DateOnly Previous { get; init; }

    public DateOnly Next { get; init; }

    public int EventCount => Days.Where(d => d.InFocus).SelectMany(d => d.Events).Select(e => e.Id).Distinct().Count();
}

public sealed class CalendarService(EventTrackerDbContext db, EventClock clock)
{
    /// <summary>
    /// Builds a month grid (whole weeks, Monday first) or a single week around <paramref name="date"/>.
    /// An event that spans midnight is shown on every day it touches.
    /// </summary>
    public async Task<CalendarPage> GetAsync(CalendarView view, DateOnly? date)
    {
        var today = DateOnly.FromDateTime(clock.Today);
        var focus = date ?? today;

        DateOnly anchor, first, last, previous, next;
        if (view == CalendarView.Week)
        {
            anchor = StartOfWeek(focus);
            first = anchor;
            last = anchor.AddDays(6);
            previous = anchor.AddDays(-7);
            next = anchor.AddDays(7);
        }
        else
        {
            anchor = new DateOnly(focus.Year, focus.Month, 1);
            first = StartOfWeek(anchor);
            last = StartOfWeek(anchor.AddMonths(1).AddDays(-1)).AddDays(6);
            previous = anchor.AddMonths(-1);
            next = anchor.AddMonths(1);
        }

        var from = first.ToDateTime(TimeOnly.MinValue);
        var to = last.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.StartsAt < to && e.EndsAt > from)
            .OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            .Select(e => new CalendarEvent(
                e.Id,
                e.Title,
                e.StartsAt,
                e.EndsAt,
                e.Location,
                e.Category!.Icon,
                e.Capacity != null && e.Attendees.Count(a => a.Status == AttendanceStatus.Confirmed) >= e.Capacity))
            .ToListAsync();

        var days = new List<CalendarDay>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var start = day.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            days.Add(new CalendarDay(
                day,
                view == CalendarView.Week || day.Month == anchor.Month,
                day == today,
                events.Where(e => e.StartsAt < end && e.EndsAt > start).ToList()));
        }

        return new CalendarPage { View = view, Anchor = anchor, Days = days, Previous = previous, Next = next };
    }

    internal static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
