using EventTracker.Application.Calendar;

namespace EventTracker.Application.Tests;

public sealed class CalendarServiceTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task Month_grid_covers_whole_weeks_starting_on_monday()
    {
        // October 2026 starts on a Thursday and ends on a Saturday.
        var page = await db.Calendar().GetAsync(CalendarView.Month, new DateOnly(2026, 10, 15));

        Assert.Equal(new DateOnly(2026, 10, 1), page.Anchor);
        Assert.Equal(new DateOnly(2026, 9, 28), page.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 1), page.Days[^1].Date);
        Assert.Equal(35, page.Days.Count);
        Assert.False(page.Days[0].InFocus);
        Assert.Equal(new DateOnly(2026, 9, 1), page.Previous);
        Assert.Equal(new DateOnly(2026, 11, 1), page.Next);
    }

    [Fact]
    public async Task Today_is_marked()
    {
        var page = await db.Calendar().GetAsync(CalendarView.Month, null);

        var today = Assert.Single(page.Days, d => d.IsToday);
        Assert.Equal(new DateOnly(2026, 10, 1), today.Date);
    }

    [Fact]
    public async Task Week_view_runs_monday_to_sunday()
    {
        var page = await db.Calendar().GetAsync(CalendarView.Week, new DateOnly(2026, 10, 4));

        Assert.Equal(new DateOnly(2026, 9, 28), page.Anchor);
        Assert.Equal(7, page.Days.Count);
        Assert.All(page.Days, d => Assert.True(d.InFocus));
        Assert.Equal(new DateOnly(2026, 9, 21), page.Previous);
        Assert.Equal(new DateOnly(2026, 10, 5), page.Next);
    }

    [Fact]
    public async Task Events_land_on_their_days_and_overnight_events_on_both()
    {
        var concert = db.AddEvent("Konser", new DateTime(2026, 10, 9, 22, 0, 0), durationHours: 4);
        db.AddEvent("Atölye", new DateTime(2026, 10, 9, 10, 0, 0));
        db.AddEvent("Kasım", new DateTime(2026, 11, 20, 10, 0, 0));

        var page = await db.Calendar().GetAsync(CalendarView.Month, new DateOnly(2026, 10, 1));

        var friday = page.Days.Single(d => d.Date == new DateOnly(2026, 10, 9));
        var saturday = page.Days.Single(d => d.Date == new DateOnly(2026, 10, 10));
        Assert.Equal(new[] { "Atölye", "Konser" }, friday.Events.Select(e => e.Title));
        Assert.Equal(concert.Id, Assert.Single(saturday.Events).Id);
        Assert.Equal(2, page.EventCount);
    }

    [Fact]
    public async Task Full_events_are_flagged()
    {
        var entity = db.AddEvent(startsAt: new DateTime(2026, 10, 5, 19, 0, 0), capacity: 1);
        db.AddAttendee(entity, "Ayşe Yılmaz");

        var page = await db.Calendar().GetAsync(CalendarView.Week, new DateOnly(2026, 10, 5));

        Assert.True(page.Days.SelectMany(d => d.Events).Single().IsFull);
    }

    [Theory]
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 11, 2026, 10, 5)]
    [InlineData(2026, 10, 7, 2026, 10, 5)]
    public void Start_of_week_is_monday(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), CalendarService.StartOfWeek(new DateOnly(y, m, d)));
    }
}
