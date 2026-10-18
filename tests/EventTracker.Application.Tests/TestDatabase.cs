using EventTracker.Application.Attendance;
using EventTracker.Application.Calendar;
using EventTracker.Application.Categories;
using EventTracker.Application.Common;
using EventTracker.Application.Events;
using EventTracker.Data;
using EventTracker.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace EventTracker.Application.Tests;

/// <summary>
/// A fresh in-memory SQLite database per test, built from the real migrations' model. The app itself runs on
/// SQLite, so constraints, transactions and ExecuteUpdate behave exactly as in production.
/// The clock starts on Thursday 1 October 2026, 09:00 in Istanbul.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();

        Context = CreateContext();
        Context.Database.Migrate();

        Clock = new EventClock(Time, EventClock.FindZone("Europe/Istanbul"));
    }

    public EventTrackerDbContext Context { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero));

    public EventClock Clock { get; }

    public DateTime Now => Clock.Now;

    public EventService Events() => new(Context, Clock);

    public AttendanceService Attendance() => new(Context, Clock, Events());

    public CalendarService Calendar() => new(Context, Clock);

    public CategoryService Categories() => new(Context, Clock);

    /// <summary>A separate context on the same database, to read what was really saved.</summary>
    public EventTrackerDbContext Fresh() => CreateContext();

    public Event AddEvent(
        string title = "Atölye",
        DateTime? startsAt = null,
        int durationHours = 2,
        int? capacity = 10,
        int categoryId = 2,
        string location = "Kadıköy",
        string description = "Uzun bir açıklama metni burada duruyor.",
        string? cover = null)
    {
        var start = startsAt ?? Now.Date.AddDays(3).AddHours(19);
        var entity = new Event
        {
            Title = title,
            Description = description,
            Location = location,
            StartsAt = start,
            EndsAt = start.AddHours(durationHours),
            Capacity = capacity,
            CategoryId = categoryId,
            CoverImagePath = cover,
            SearchText = SearchFolding.Fold($"{title} {location} {description}"),
            CreatedAt = Now.AddDays(-10),
            UpdatedAt = Now.AddDays(-10),
        };
        Context.Events.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    public Attendee AddAttendee(Event entity, string name, AttendanceStatus status = AttendanceStatus.Confirmed, int minutesAgo = 60)
    {
        var attendee = new Attendee
        {
            EventId = entity.Id,
            FullName = name,
            Email = $"{SearchFolding.Fold(name).Replace(' ', '.')}@example.com",
            Status = status,
            RegisteredAt = Now.AddMinutes(-minutesAgo),
            AccessToken = Tokens.Create(),
        };
        Context.Attendees.Add(attendee);
        Context.SaveChanges();
        return attendee;
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }

    private EventTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<EventTrackerDbContext>().UseSqlite(connection).Options);
}
