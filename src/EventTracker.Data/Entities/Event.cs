namespace EventTracker.Data.Entities;

public class Event
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Local wall-clock time in the app's time zone (see <c>Events:TimeZone</c>).</summary>
    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string Location { get; set; } = string.Empty;

    /// <summary>Maximum confirmed attendees; null means no limit.</summary>
    public int? Capacity { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Path under wwwroot, e.g. "/uploads/covers/abc.webp".</summary>
    public string? CoverImagePath { get; set; }

    /// <summary>Title, location and description folded to plain lower-case ASCII for searching.</summary>
    public string SearchText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Attendee> Attendees { get; set; } = [];
}
