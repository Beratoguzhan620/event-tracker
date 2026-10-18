using EventTracker.Data.Entities;

namespace EventTracker.Data;

/// <summary>Fixed categories, created by the initial migration.</summary>
public static class CategorySeed
{
    public static readonly Category[] All =
    [
        new() { Id = 1, Name = "Konferans", Slug = "konferans", Icon = "mic" },
        new() { Id = 2, Name = "Atölye", Slug = "atolye", Icon = "tools" },
        new() { Id = 3, Name = "Seminer", Slug = "seminer", Icon = "easel" },
        new() { Id = 4, Name = "Buluşma", Slug = "bulusma", Icon = "people" },
        new() { Id = 5, Name = "Konser", Slug = "konser", Icon = "music-note-beamed" },
        new() { Id = 6, Name = "Spor", Slug = "spor", Icon = "bicycle" },
    ];
}
