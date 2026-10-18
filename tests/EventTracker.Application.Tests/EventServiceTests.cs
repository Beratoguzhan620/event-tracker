using EventTracker.Application.Common;
using EventTracker.Application.Events;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    private EventInput Input(DateTime? start = null, int hours = 2, int? capacity = 20, int categoryId = 2) => new()
    {
        Title = "  Ekşi maya atölyesi  ",
        Description = "  Maya beslemekten fırına kadar bütün süreç.  ",
        Location = " Kadıköy · Fırın Mutfak ",
        StartsAt = start ?? db.Now.Date.AddDays(5).AddHours(13).AddSeconds(42),
        EndsAt = (start ?? db.Now.Date.AddDays(5).AddHours(13)).AddHours(hours),
        Capacity = capacity,
        CategoryId = categoryId,
    };

    [Fact]
    public async Task Create_saves_trimmed_values_and_drops_seconds()
    {
        var result = await db.Events().CreateAsync(Input());

        Assert.True(result.Success, result.Message);
        var saved = await db.Fresh().Events.SingleAsync(e => e.Id == result.Value!.Id);
        Assert.Equal("Ekşi maya atölyesi", saved.Title);
        Assert.Equal("Kadıköy · Fırın Mutfak", saved.Location);
        Assert.Equal(0, saved.StartsAt.Second);
        Assert.Contains("kadikoy", saved.SearchText);
    }

    [Fact]
    public async Task Create_refuses_a_start_in_the_past()
    {
        var result = await db.Events().CreateAsync(Input(start: db.Now.AddHours(-1)));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(nameof(EventInput.StartsAt), result.Field);
    }

    [Fact]
    public async Task Create_refuses_an_end_before_the_start()
    {
        var input = Input() with { EndsAt = Input().StartsAt.AddMinutes(-30) };

        var result = await db.Events().CreateAsync(input);

        Assert.Equal(nameof(EventInput.EndsAt), result.Field);
    }

    [Fact]
    public async Task Create_refuses_events_longer_than_two_weeks()
    {
        var result = await db.Events().CreateAsync(Input(hours: 24 * 15));

        Assert.Equal(nameof(EventInput.EndsAt), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(EventService.MaxCapacity + 1)]
    public async Task Create_refuses_capacity_out_of_range(int capacity)
    {
        var result = await db.Events().CreateAsync(Input(capacity: capacity));

        Assert.Equal(nameof(EventInput.Capacity), result.Field);
    }

    [Fact]
    public async Task Create_refuses_an_unknown_category()
    {
        var result = await db.Events().CreateAsync(Input(categoryId: 99));

        Assert.Equal(nameof(EventInput.CategoryId), result.Field);
    }

    [Fact]
    public async Task Upcoming_keeps_running_events_and_sorts_by_start()
    {
        db.AddEvent("Bitti", db.Now.AddHours(-5), durationHours: 2);
        db.AddEvent("Sürüyor", db.Now.AddHours(-1), durationHours: 3);
        db.AddEvent("Yarın", db.Now.AddDays(1));
        db.AddEvent("Bugün akşam", db.Now.AddHours(8));

        var list = await db.Events().ListAsync(new EventListQuery());

        Assert.Equal(new[] { "Sürüyor", "Bugün akşam", "Yarın" }, list.Items.Select(e => e.Title));
    }

    [Fact]
    public async Task Past_lists_finished_events_newest_first()
    {
        db.AddEvent("Geçen ay", db.Now.AddDays(-30));
        db.AddEvent("Dün", db.Now.AddDays(-1));
        db.AddEvent("Gelecek", db.Now.AddDays(1));

        var list = await db.Events().ListAsync(new EventListQuery { Scope = EventScope.Past });

        Assert.Equal(new[] { "Dün", "Geçen ay" }, list.Items.Select(e => e.Title));
    }

    [Theory]
    [InlineData("İSTANBUL")]
    [InlineData("istanbul")]
    [InlineData("Istanbul")]
    public async Task Search_ignores_turkish_case(string query)
    {
        db.AddEvent("Frontend İstanbul buluşması");
        db.AddEvent("Ankara buluşması");

        var list = await db.Events().ListAsync(new EventListQuery { Search = query });

        Assert.Equal("Frontend İstanbul buluşması", Assert.Single(list.Items).Title);
    }

    [Fact]
    public async Task Search_matches_location_without_accents_and_needs_every_word()
    {
        db.AddEvent("Seramik", location: "Kadıköy · Moda");
        db.AddEvent("Resim", location: "Kadıköy · Yeldeğirmeni");

        var one = await db.Events().ListAsync(new EventListQuery { Search = "kadikoy moda" });
        var both = await db.Events().ListAsync(new EventListQuery { Search = "kadıköy" });

        Assert.Equal("Seramik", Assert.Single(one.Items).Title);
        Assert.Equal(2, both.TotalCount);
    }

    [Fact]
    public async Task Category_filter_uses_the_slug()
    {
        db.AddEvent("Konser", categoryId: 5);
        db.AddEvent("Atölye", categoryId: 2);

        var list = await db.Events().ListAsync(new EventListQuery { Category = "konser" });

        Assert.Equal("Konser", Assert.Single(list.Items).Title);
    }

    [Fact]
    public async Task List_counts_seats_and_waiting_list_separately()
    {
        var entity = db.AddEvent(capacity: 2);
        db.AddAttendee(entity, "Ayşe Yılmaz");
        db.AddAttendee(entity, "Mehmet Kaya");
        db.AddAttendee(entity, "Elif Demir", AttendanceStatus.Waitlisted);
        db.AddAttendee(entity, "Can Koç", AttendanceStatus.Cancelled);

        var item = Assert.Single((await db.Events().ListAsync(new EventListQuery())).Items);

        Assert.Equal(2, item.ConfirmedCount);
        Assert.Equal(1, item.WaitlistCount);
        Assert.True(item.IsFull);
    }

    [Fact]
    public async Task Details_hide_cancelled_registrations()
    {
        var entity = db.AddEvent();
        db.AddAttendee(entity, "Ayşe Yılmaz");
        db.AddAttendee(entity, "Can Koç", AttendanceStatus.Cancelled);

        var details = await db.Events().GetAsync(entity.Id);

        Assert.Equal("Ayşe Yılmaz", Assert.Single(details!.Confirmed).FullName);
        Assert.Empty(details.Waitlist);
    }

    [Fact]
    public async Task Update_refuses_capacity_below_confirmed_attendees()
    {
        var entity = db.AddEvent(capacity: 5);
        db.AddAttendee(entity, "Ayşe Yılmaz");
        db.AddAttendee(entity, "Mehmet Kaya");

        var result = await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { Capacity = 1 });

        Assert.Equal(nameof(EventInput.Capacity), result.Field);
        Assert.Contains("(2)", result.Message);
    }

    [Fact]
    public async Task Raising_capacity_moves_people_off_the_waiting_list_in_order()
    {
        var entity = db.AddEvent(capacity: 1);
        db.AddAttendee(entity, "İlk Gelen", minutesAgo: 300);
        var second = db.AddAttendee(entity, "İkinci", AttendanceStatus.Waitlisted, minutesAgo: 200);
        var third = db.AddAttendee(entity, "Üçüncü", AttendanceStatus.Waitlisted, minutesAgo: 100);

        var result = await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { Capacity = 2 });

        Assert.True(result.Success, result.Message);
        Assert.Contains("1 kişi", result.Message);
        var fresh = db.Fresh();
        Assert.Equal(AttendanceStatus.Confirmed, (await fresh.Attendees.FindAsync(second.Id))!.Status);
        Assert.Equal(AttendanceStatus.Waitlisted, (await fresh.Attendees.FindAsync(third.Id))!.Status);
    }

    [Fact]
    public async Task Removing_the_limit_confirms_everyone_waiting()
    {
        var entity = db.AddEvent(capacity: 1);
        db.AddAttendee(entity, "İlk Gelen");
        db.AddAttendee(entity, "İkinci", AttendanceStatus.Waitlisted);
        db.AddAttendee(entity, "Üçüncü", AttendanceStatus.Waitlisted);

        await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { Capacity = null });

        Assert.Equal(3, await db.Fresh().Attendees.CountAsync(a => a.Status == AttendanceStatus.Confirmed));
    }

    [Fact]
    public async Task Update_may_keep_a_start_time_that_has_passed()
    {
        var entity = db.AddEvent(startsAt: db.Now.AddHours(-1), durationHours: 3);

        var result = await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { Title = "Yeni ad" });

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task Replacing_or_removing_the_cover_returns_the_old_file()
    {
        var entity = db.AddEvent(cover: "/uploads/covers/eski.jpg");

        var replaced = await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { NewCoverPath = "/uploads/covers/yeni.jpg" });
        var removed = await db.Events().UpdateAsync(entity.Id, InputFor(entity) with { RemoveCover = true });
        var kept = await db.Events().UpdateAsync(entity.Id, InputFor(entity));

        Assert.Equal("/uploads/covers/eski.jpg", replaced.Value!.ReplacedCoverPath);
        Assert.Equal("/uploads/covers/yeni.jpg", removed.Value!.ReplacedCoverPath);
        Assert.Null(kept.Value!.ReplacedCoverPath);
        Assert.Null((await db.Fresh().Events.FindAsync(entity.Id))!.CoverImagePath);
    }

    [Fact]
    public async Task Delete_removes_attendees_and_returns_the_cover()
    {
        var entity = db.AddEvent(cover: "/uploads/covers/kapak.webp");
        db.AddAttendee(entity, "Ayşe Yılmaz");

        var result = await db.Events().DeleteAsync(entity.Id);

        Assert.Equal("/uploads/covers/kapak.webp", result.Value);
        Assert.Equal(0, await db.Fresh().Attendees.CountAsync());
    }

    [Fact]
    public async Task Unknown_events_are_not_found()
    {
        Assert.Null(await db.Events().GetAsync(404));
        Assert.Equal(ResultStatus.NotFound, (await db.Events().DeleteAsync(404)).Status);
        Assert.Equal(ResultStatus.NotFound, (await db.Events().UpdateAsync(404, Input())).Status);
    }

    [Fact]
    public async Task Categories_count_upcoming_events()
    {
        db.AddEvent("Atölye 1", categoryId: 2);
        db.AddEvent("Atölye 2", categoryId: 2);
        db.AddEvent("Eski atölye", db.Now.AddDays(-5), categoryId: 2);

        var categories = await db.Categories().ListAsync();

        Assert.Equal(6, categories.Count);
        Assert.Equal(2, categories.Single(c => c.Slug == "atolye").UpcomingCount);
    }

    private static EventInput InputFor(Event entity) => new()
    {
        Title = entity.Title,
        Description = entity.Description,
        Location = entity.Location,
        StartsAt = entity.StartsAt,
        EndsAt = entity.EndsAt,
        Capacity = entity.Capacity,
        CategoryId = entity.CategoryId,
    };
}
