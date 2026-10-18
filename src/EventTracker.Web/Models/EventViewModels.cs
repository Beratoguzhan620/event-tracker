using System.ComponentModel.DataAnnotations;
using EventTracker.Application.Categories;
using EventTracker.Application.Common;
using EventTracker.Application.Events;
using EventTracker.Data;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventTracker.Web.Models;

public sealed class EventIndexViewModel
{
    public required PagedList<EventListItem> Result { get; init; }

    public required EventListQuery Query { get; init; }

    public required List<CategoryItem> Categories { get; init; }

    public required DateTime Now { get; init; }

    public PagerViewModel Pager => new(Result.Page, Result.PageSize, Result.TotalCount, Result.TotalPages);

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Query.Search) || !string.IsNullOrWhiteSpace(Query.Category);
}

public sealed class EventDetailsViewModel
{
    public required EventDetails Event { get; init; }

    public required DateTime Now { get; init; }

    public bool IsPast => Event.EndsAt < Now;

    public bool IsOngoing => Event.StartsAt <= Now && Now < Event.EndsAt;
}

public sealed class EventFormModel
{
    public int? Id { get; set; }

    [Display(Name = "Başlık")]
    [Required(ErrorMessage = "Başlık zorunlu.")]
    [StringLength(Limits.Title, ErrorMessage = "Başlık en fazla {1} karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Kategori")]
    [Required(ErrorMessage = "Bir kategori seçin.")]
    public int? CategoryId { get; set; }

    [Display(Name = "Başlangıç tarihi")]
    [Required(ErrorMessage = "Başlangıç tarihi zorunlu.")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "Başlangıç saati")]
    [Required(ErrorMessage = "Başlangıç saati zorunlu.")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "Bitiş tarihi")]
    [Required(ErrorMessage = "Bitiş tarihi zorunlu.")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Bitiş saati")]
    [Required(ErrorMessage = "Bitiş saati zorunlu.")]
    public TimeOnly? EndTime { get; set; }

    [Display(Name = "Konum")]
    [Required(ErrorMessage = "Konum zorunlu.")]
    [StringLength(Limits.Location, ErrorMessage = "Konum en fazla {1} karakter olabilir.")]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Kapasite")]
    [Range(1, EventService.MaxCapacity, ErrorMessage = "Kapasite 1 ile 100.000 arasında olmalı.")]
    public int? Capacity { get; set; }

    [Display(Name = "Açıklama")]
    [Required(ErrorMessage = "Açıklama zorunlu.")]
    [StringLength(Limits.Description, MinimumLength = 20, ErrorMessage = "Açıklama {2} ile {1} karakter arasında olmalı.")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Kapak görseli")]
    public IFormFile? Cover { get; set; }

    public bool RemoveCover { get; set; }

    public string? ExistingCoverPath { get; set; }

    /// <summary>Confirmed attendees on an existing event; capacity cannot go below it.</summary>
    public int ConfirmedCount { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];

    public bool IsEdit => Id.HasValue;

    public DateTime StartsAt => StartDate!.Value.ToDateTime(StartTime!.Value);

    public DateTime EndsAt => EndDate!.Value.ToDateTime(EndTime!.Value);

    public static EventFormModel ForNew(DateTime now)
    {
        var start = now.Date.AddDays(7).AddHours(19);
        return new EventFormModel
        {
            StartDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndDate = DateOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(start.AddHours(2)),
        };
    }

    public static EventFormModel From(EventDetails details) => new()
    {
        Id = details.Id,
        Title = details.Title,
        CategoryId = details.CategoryId,
        StartDate = DateOnly.FromDateTime(details.StartsAt),
        StartTime = TimeOnly.FromDateTime(details.StartsAt),
        EndDate = DateOnly.FromDateTime(details.EndsAt),
        EndTime = TimeOnly.FromDateTime(details.EndsAt),
        Location = details.Location,
        Capacity = details.Capacity,
        Description = details.Description,
        ExistingCoverPath = details.CoverImagePath,
        ConfirmedCount = details.ConfirmedCount,
    };

    /// <summary>Maps a service field name to the form field that should show the message.</summary>
    public static string FieldFor(string? serviceField) => serviceField switch
    {
        nameof(EventInput.StartsAt) => nameof(StartDate),
        nameof(EventInput.EndsAt) => nameof(EndDate),
        null => string.Empty,
        _ => serviceField,
    };
}
