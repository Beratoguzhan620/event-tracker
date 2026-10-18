using EventTracker.Application.Attendance;
using EventTracker.Application.Calendar;

namespace EventTracker.Web.Models;

public sealed class AttendeesViewModel
{
    public required AttendeeList List { get; init; }

    public required bool IsPast { get; init; }
}

public sealed class RegistrationViewModel
{
    public required RegistrationView Registration { get; init; }

    public required DateTime Now { get; init; }

    /// <summary>True right after signing up, so the page can stress that the link should be kept.</summary>
    public bool JustRegistered { get; init; }

    public bool IsPast => Registration.EndsAt <= Now;
}

public sealed class CalendarViewModel
{
    public required CalendarPage Page { get; init; }

    public required DateOnly Today { get; init; }
}
