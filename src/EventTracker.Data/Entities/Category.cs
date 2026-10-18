namespace EventTracker.Data.Entities;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-case, URL-friendly key used in filters, e.g. "atolye".</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Bootstrap Icons name without the "bi-" prefix.</summary>
    public string Icon { get; set; } = "calendar-event";

    public List<Event> Events { get; set; } = [];
}
