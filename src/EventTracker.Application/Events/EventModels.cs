using EventTracker.Data.Entities;

namespace EventTracker.Application.Events;

public enum EventScope
{
    Upcoming,
    Past,
    All,
}

public sealed record EventListQuery
{
    public EventScope Scope { get; init; } = EventScope.Upcoming;

    public string? Search { get; init; }

    public string? Category { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 12;
}

public sealed record EventListItem
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public string Location { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string CategoryIcon { get; init; } = string.Empty;
    public int? Capacity { get; init; }
    public int ConfirmedCount { get; init; }
    public int WaitlistCount { get; init; }
    public string? CoverImagePath { get; init; }

    public bool IsFull => Capacity is int capacity && ConfirmedCount >= capacity;
}

public sealed record AttendeeItem(int Id, string FullName, string Email, AttendanceStatus Status, DateTime RegisteredAt);

public sealed record EventDetails
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public string Location { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;
    public string CategoryIcon { get; init; } = string.Empty;
    public int? Capacity { get; init; }
    public string? CoverImagePath { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<AttendeeItem> Confirmed { get; init; } = [];
    public List<AttendeeItem> Waitlist { get; init; } = [];

    public int ConfirmedCount => Confirmed.Count;
    public bool IsFull => Capacity is int capacity && ConfirmedCount >= capacity;
    public int? SeatsLeft => Capacity is int capacity ? Math.Max(0, capacity - ConfirmedCount) : null;
}

/// <summary>Values from the create/edit form, already parsed.</summary>
public sealed record EventInput
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public string Location { get; init; } = string.Empty;
    public int? Capacity { get; init; }
    public int CategoryId { get; init; }

    /// <summary>Path of a newly uploaded cover; null keeps the current one.</summary>
    public string? NewCoverPath { get; init; }

    public bool RemoveCover { get; init; }
}

/// <summary>Result of saving an event: its id and a cover file that is no longer used and can be deleted.</summary>
public sealed record EventSaved(int Id, string? ReplacedCoverPath);
