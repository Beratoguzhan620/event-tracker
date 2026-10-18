using EventTracker.Data;
using EventTracker.Data.Entities;
using EventTracker.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Tests;

public sealed class SearchAndSeedTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Theory]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("Işık", "isik")]
    [InlineData("  Kadıköy   Moda ", "kadikoy   moda")]
    [InlineData("Çağrı Öztürk", "cagri ozturk")]
    [InlineData(null, "")]
    public void Folding_turns_turkish_text_into_plain_ascii(string? input, string expected)
    {
        Assert.Equal(expected, SearchFolding.Fold(input));
    }

    [Fact]
    public async Task Demo_data_is_consistent_and_seeded_once()
    {
        await DemoDataSeeder.SeedAsync(db.Context, db.Now);
        await DemoDataSeeder.SeedAsync(db.Context, db.Now);

        var fresh = db.Fresh();
        var events = await fresh.Events.Include(e => e.Attendees).ToListAsync();

        Assert.Equal(18, events.Count);
        Assert.Contains(events, e => e.EndsAt < db.Now);
        Assert.Contains(events, e => e.StartsAt > db.Now);
        foreach (var item in events)
        {
            var confirmed = item.Attendees.Count(a => a.Status == AttendanceStatus.Confirmed);
            var waiting = item.Attendees.Count(a => a.Status == AttendanceStatus.Waitlisted);
            Assert.True(item.Capacity is null || confirmed <= item.Capacity, item.Title);
            Assert.True(waiting == 0 || confirmed == item.Capacity, $"{item.Title} has a waiting list but free seats");
            Assert.Equal(item.Attendees.Count, item.Attendees.Select(a => a.Email).Distinct().Count());
            Assert.All(item.Attendees, a => Assert.True(a.RegisteredAt <= db.Now));
            Assert.False(string.IsNullOrEmpty(item.SearchText));
        }
        Assert.Equal(events.SelectMany(e => e.Attendees).Count(), events.SelectMany(e => e.Attendees).Select(a => a.AccessToken).Distinct().Count());
    }
}
