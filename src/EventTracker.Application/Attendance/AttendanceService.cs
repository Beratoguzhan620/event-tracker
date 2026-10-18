using System.Net.Mail;
using EventTracker.Application.Common;
using EventTracker.Application.Events;
using EventTracker.Data;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Attendance;

/// <summary>
/// Registrations for events: a seat while there is room, otherwise the waiting list. A cancelled seat goes to
/// the first person waiting.
/// </summary>
public sealed class AttendanceService(EventTrackerDbContext db, EventClock clock, EventService events)
{
    public async Task<Result<Registration>> RegisterAsync(int eventId, RegistrationInput input)
    {
        var fullName = input.FullName?.Trim() ?? string.Empty;
        var email = NormalizeEmail(input.Email);

        if (fullName.Length < 2)
            return Result<Registration>.Invalid("Adınızı ve soyadınızı yazın.", nameof(RegistrationInput.FullName));
        if (fullName.Length > Limits.FullName)
            return Result<Registration>.Invalid($"Ad soyad en fazla {Limits.FullName} karakter olabilir.", nameof(RegistrationInput.FullName));
        if (email is null)
            return Result<Registration>.Invalid("Geçerli bir e-posta adresi yazın.", nameof(RegistrationInput.Email));

        await using var transaction = await db.Database.BeginTransactionAsync();

        // Touching the event row first takes SQLite's write lock, so two people cannot both read
        // "one seat left" and take it at the same time.
        var touched = await db.Events
            .Where(e => e.Id == eventId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UpdatedAt, e => e.UpdatedAt));
        if (touched == 0)
            return Result<Registration>.NotFound("Etkinlik bulunamadı.");

        var entity = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        if (entity.EndsAt <= clock.Now)
            return Result<Registration>.Invalid("Bu etkinlik sona erdi; kayıt alınmıyor.");

        var existing = await db.Attendees.FirstOrDefaultAsync(a => a.EventId == eventId && a.Email == email);
        if (existing is not null && existing.Status != AttendanceStatus.Cancelled)
        {
            return Result<Registration>.Invalid(
                existing.Status == AttendanceStatus.Confirmed
                    ? "Bu e-posta adresiyle zaten kayıtlısınız."
                    : "Bu e-posta adresi zaten bekleme listesinde.",
                nameof(RegistrationInput.Email));
        }

        var confirmed = await db.Attendees.CountAsync(a => a.EventId == eventId && a.Status == AttendanceStatus.Confirmed);
        var waiting = await db.Attendees.CountAsync(a => a.EventId == eventId && a.Status == AttendanceStatus.Waitlisted);
        var hasSeat = entity.Capacity is not int capacity || (confirmed < capacity && waiting == 0);

        var attendee = existing ?? new Attendee { EventId = eventId, Email = email };
        attendee.FullName = fullName;
        attendee.Status = hasSeat ? AttendanceStatus.Confirmed : AttendanceStatus.Waitlisted;
        attendee.RegisteredAt = clock.Now;
        attendee.CancelledAt = null;
        attendee.AccessToken = Tokens.Create();
        if (existing is null)
            db.Attendees.Add(attendee);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var message = hasSeat
            ? "Kaydınız alındı, yeriniz ayrıldı."
            : $"Kontenjan dolu; bekleme listesinde {waiting + 1}. sıradasınız. Yer açılınca otomatik olarak katılımcılara alınırsınız.";
        return Result<Registration>.Ok(
            new Registration(attendee.Id, attendee.Status, hasSeat ? null : waiting + 1, attendee.AccessToken), message);
    }

    /// <summary>Cancels a registration by its personal link key.</summary>
    public async Task<Result> CancelByTokenAsync(string token)
    {
        var attendee = await db.Attendees.FirstOrDefaultAsync(a => a.AccessToken == token);
        return attendee is null ? Result.NotFound("Kayıt bulunamadı.") : await CancelAsync(attendee);
    }

    /// <summary>Cancels a registration from the organiser's attendee list.</summary>
    public async Task<Result> CancelByIdAsync(int eventId, int attendeeId)
    {
        var attendee = await db.Attendees.FirstOrDefaultAsync(a => a.Id == attendeeId && a.EventId == eventId);
        return attendee is null ? Result.NotFound("Kayıt bulunamadı.") : await CancelAsync(attendee);
    }

    public async Task<RegistrationView?> GetByTokenAsync(string token)
    {
        var attendee = await db.Attendees
            .AsNoTracking()
            .Include(a => a.Event)
            .FirstOrDefaultAsync(a => a.AccessToken == token);
        if (attendee is null)
            return null;

        int? position = null;
        if (attendee.Status == AttendanceStatus.Waitlisted)
        {
            position = await db.Attendees.CountAsync(a =>
                a.EventId == attendee.EventId &&
                a.Status == AttendanceStatus.Waitlisted &&
                (a.RegisteredAt < attendee.RegisteredAt || (a.RegisteredAt == attendee.RegisteredAt && a.Id < attendee.Id))) + 1;
        }

        return new RegistrationView
        {
            AttendeeId = attendee.Id,
            FullName = attendee.FullName,
            Email = attendee.Email,
            Status = attendee.Status,
            RegisteredAt = attendee.RegisteredAt,
            CancelledAt = attendee.CancelledAt,
            WaitlistPosition = position,
            EventId = attendee.EventId,
            EventTitle = attendee.Event!.Title,
            StartsAt = attendee.Event.StartsAt,
            EndsAt = attendee.Event.EndsAt,
            Location = attendee.Event.Location,
        };
    }

    public async Task<AttendeeList?> ListAsync(int eventId)
    {
        var entity = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (entity is null)
            return null;

        var rows = await db.Attendees
            .AsNoTracking()
            .Where(a => a.EventId == eventId)
            .OrderBy(a => a.RegisteredAt).ThenBy(a => a.Id)
            .Select(a => new AttendeeRow(a.Id, a.FullName, a.Email, a.Status, a.RegisteredAt, a.CancelledAt))
            .ToListAsync();

        return new AttendeeList(
            entity.Id,
            entity.Title,
            entity.Capacity,
            rows.Where(r => r.Status == AttendanceStatus.Confirmed).ToList(),
            rows.Where(r => r.Status == AttendanceStatus.Waitlisted).ToList(),
            rows.Where(r => r.Status == AttendanceStatus.Cancelled).OrderByDescending(r => r.CancelledAt).ToList());
    }

    private async Task<Result> CancelAsync(Attendee attendee)
    {
        if (attendee.Status == AttendanceStatus.Cancelled)
            return Result.Invalid("Bu kayıt zaten iptal edilmiş.");

        var entity = await db.Events.AsNoTracking().FirstAsync(e => e.Id == attendee.EventId);
        if (entity.EndsAt <= clock.Now)
            return Result.Invalid("Sona eren bir etkinliğin kaydı iptal edilemez.");

        await using var transaction = await db.Database.BeginTransactionAsync();

        var wasConfirmed = attendee.Status == AttendanceStatus.Confirmed;
        attendee.Status = AttendanceStatus.Cancelled;
        attendee.CancelledAt = clock.Now;
        await db.SaveChangesAsync();

        var promoted = 0;
        if (wasConfirmed)
        {
            var confirmed = await db.Attendees.CountAsync(a => a.EventId == attendee.EventId && a.Status == AttendanceStatus.Confirmed);
            promoted = await events.PromoteWaitlistAsync(attendee.EventId, entity.Capacity, confirmed);
        }

        await transaction.CommitAsync();

        return Result.Ok(promoted > 0
            ? $"{attendee.FullName} adlı kişinin kaydı iptal edildi; bekleme listesindeki ilk kişi katılımcılara alındı."
            : $"{attendee.FullName} adlı kişinin kaydı iptal edildi.");
    }

    internal static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > Limits.Email)
            return null;

        return MailAddress.TryCreate(trimmed, out var address) && address.Address == trimmed && trimmed.Contains('.', StringComparison.Ordinal)
            ? trimmed
            : null;
    }
}
