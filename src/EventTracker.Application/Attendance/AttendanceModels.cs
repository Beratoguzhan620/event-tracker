using EventTracker.Data.Entities;

namespace EventTracker.Application.Attendance;

public sealed record RegistrationInput(string FullName, string Email);

/// <summary>Outcome of a registration: where the person landed and their personal link key.</summary>
public sealed record Registration(int AttendeeId, AttendanceStatus Status, int? WaitlistPosition, string AccessToken);

public sealed record RegistrationView
{
    public int AttendeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public AttendanceStatus Status { get; init; }
    public DateTime RegisteredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public int? WaitlistPosition { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public string Location { get; init; } = string.Empty;
}

public sealed record AttendeeRow(
    int Id,
    string FullName,
    string Email,
    AttendanceStatus Status,
    DateTime RegisteredAt,
    DateTime? CancelledAt);

public sealed record AttendeeList(int EventId, string EventTitle, int? Capacity, List<AttendeeRow> Confirmed, List<AttendeeRow> Waitlist, List<AttendeeRow> Cancelled);
