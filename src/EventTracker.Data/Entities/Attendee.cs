namespace EventTracker.Data.Entities;

public enum AttendanceStatus
{
    Confirmed = 1,
    Waitlisted = 2,
    Cancelled = 3,
}

public class Attendee
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Stored trimmed and lower-cased so one person can only register once per event.</summary>
    public string Email { get; set; } = string.Empty;

    public AttendanceStatus Status { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    /// <summary>Random key in the personal link that lets the attendee see or cancel their registration.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
