using EventTracker.Application.Common;
using EventTracker.Data;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Events;

public sealed class EventService(EventTrackerDbContext db, EventClock clock)
{
    public const int MaxCapacity = 100_000;

    public async Task<PagedList<EventListItem>> ListAsync(EventListQuery query)
    {
        var now = clock.Now;
        var events = db.Events.AsNoTracking();

        events = query.Scope switch
        {
            EventScope.Upcoming => events.Where(e => e.EndsAt >= now),
            EventScope.Past => events.Where(e => e.EndsAt < now),
            _ => events,
        };

        var search = SearchFolding.Fold(query.Search);
        if (search.Length > 0)
        {
            foreach (var word in search.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                events = events.Where(e => e.SearchText.Contains(word));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            events = events.Where(e => e.Category!.Slug == query.Category);

        events = query.Scope == EventScope.Upcoming
            ? events.OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            : events.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.Id);

        return await events
            .Select(e => new EventListItem
            {
                Id = e.Id,
                Title = e.Title,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                Location = e.Location,
                CategoryName = e.Category!.Name,
                CategoryIcon = e.Category.Icon,
                Capacity = e.Capacity,
                ConfirmedCount = e.Attendees.Count(a => a.Status == AttendanceStatus.Confirmed),
                WaitlistCount = e.Attendees.Count(a => a.Status == AttendanceStatus.Waitlisted),
                CoverImagePath = e.CoverImagePath,
            })
            .ToPagedListAsync(query.Page, query.PageSize);
    }

    public async Task<EventDetails?> GetAsync(int id)
    {
        var entity = await db.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Attendees.Where(a => a.Status != AttendanceStatus.Cancelled))
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity is null)
            return null;

        List<AttendeeItem> ByStatus(AttendanceStatus status) => entity.Attendees
            .Where(a => a.Status == status)
            .OrderBy(a => a.RegisteredAt).ThenBy(a => a.Id)
            .Select(a => new AttendeeItem(a.Id, a.FullName, a.Email, a.Status, a.RegisteredAt))
            .ToList();

        return new EventDetails
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            StartsAt = entity.StartsAt,
            EndsAt = entity.EndsAt,
            Location = entity.Location,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category!.Name,
            CategorySlug = entity.Category.Slug,
            CategoryIcon = entity.Category.Icon,
            Capacity = entity.Capacity,
            CoverImagePath = entity.CoverImagePath,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Confirmed = ByStatus(AttendanceStatus.Confirmed),
            Waitlist = ByStatus(AttendanceStatus.Waitlisted),
        };
    }

    public async Task<Result<EventSaved>> CreateAsync(EventInput input)
    {
        var error = await ValidateAsync(input);
        if (error is not null)
            return error;

        if (input.StartsAt <= clock.Now)
            return Result<EventSaved>.Invalid("Yeni bir etkinlik geçmiş bir saatte başlayamaz.", nameof(EventInput.StartsAt));

        var now = clock.Now;
        var entity = new Event { CreatedAt = now };
        Apply(entity, input, now);
        entity.CoverImagePath = input.RemoveCover ? null : input.NewCoverPath;

        db.Events.Add(entity);
        await db.SaveChangesAsync();
        return Result<EventSaved>.Ok(new EventSaved(entity.Id, null), "Etkinlik oluşturuldu.");
    }

    public async Task<Result<EventSaved>> UpdateAsync(int id, EventInput input)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (entity is null)
            return Result<EventSaved>.NotFound("Etkinlik bulunamadı.");

        var error = await ValidateAsync(input);
        if (error is not null)
            return error;

        var confirmed = await db.Attendees.CountAsync(a => a.EventId == id && a.Status == AttendanceStatus.Confirmed);
        if (input.Capacity is int capacity && capacity < confirmed)
        {
            return Result<EventSaved>.Invalid(
                $"Kapasite, onaylı katılımcı sayısının ({confirmed}) altına indirilemez.", nameof(EventInput.Capacity));
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        Apply(entity, input, clock.Now);

        string? replaced = null;
        if (input.RemoveCover || input.NewCoverPath is not null)
        {
            replaced = entity.CoverImagePath;
            entity.CoverImagePath = input.RemoveCover ? null : input.NewCoverPath;
        }

        await db.SaveChangesAsync();
        var promoted = await PromoteWaitlistAsync(entity.Id, input.Capacity, confirmed);
        await transaction.CommitAsync();

        var message = promoted > 0
            ? $"Etkinlik güncellendi. Bekleme listesinden {promoted} kişi katılımcılara alındı."
            : "Etkinlik güncellendi.";
        return Result<EventSaved>.Ok(new EventSaved(entity.Id, replaced), message);
    }

    /// <summary>Deletes the event with its attendees and returns the cover path so the file can be removed.</summary>
    public async Task<Result<string?>> DeleteAsync(int id)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (entity is null)
            return Result<string?>.NotFound("Etkinlik bulunamadı.");

        db.Events.Remove(entity);
        await db.SaveChangesAsync();
        return Result<string?>.Ok(entity.CoverImagePath, $"\"{entity.Title}\" silindi.");
    }

    /// <summary>Moves people from the waiting list into free seats, first come first served.</summary>
    internal async Task<int> PromoteWaitlistAsync(int eventId, int? capacity, int confirmed)
    {
        var free = capacity is int limit ? limit - confirmed : int.MaxValue;
        if (free <= 0)
            return 0;

        var waiting = await db.Attendees
            .Where(a => a.EventId == eventId && a.Status == AttendanceStatus.Waitlisted)
            .OrderBy(a => a.RegisteredAt).ThenBy(a => a.Id)
            .Take(free)
            .ToListAsync();

        foreach (var attendee in waiting)
            attendee.Status = AttendanceStatus.Confirmed;

        await db.SaveChangesAsync();
        return waiting.Count;
    }

    private async Task<Result<EventSaved>?> ValidateAsync(EventInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return Result<EventSaved>.Invalid("Başlık zorunlu.", nameof(EventInput.Title));

        if (input.EndsAt <= input.StartsAt)
            return Result<EventSaved>.Invalid("Bitiş, başlangıçtan sonra olmalı.", nameof(EventInput.EndsAt));

        if ((input.EndsAt - input.StartsAt).TotalDays > 14)
            return Result<EventSaved>.Invalid("Bir etkinlik en fazla 14 gün sürebilir.", nameof(EventInput.EndsAt));

        if (input.Capacity is < 1 or > MaxCapacity)
            return Result<EventSaved>.Invalid($"Kapasite 1 ile {MaxCapacity:N0} arasında olmalı.", nameof(EventInput.Capacity));

        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId))
            return Result<EventSaved>.Invalid("Bir kategori seçin.", nameof(EventInput.CategoryId));

        return null;
    }

    private static void Apply(Event entity, EventInput input, DateTime now)
    {
        entity.Title = input.Title.Trim();
        entity.Description = input.Description.Trim();
        entity.Location = input.Location.Trim();
        entity.StartsAt = TrimSeconds(input.StartsAt);
        entity.EndsAt = TrimSeconds(input.EndsAt);
        entity.Capacity = input.Capacity;
        entity.CategoryId = input.CategoryId;
        entity.SearchText = SearchFolding.Fold($"{entity.Title} {entity.Location} {entity.Description}");
        entity.UpdatedAt = now;
    }

    private static DateTime TrimSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);
}
