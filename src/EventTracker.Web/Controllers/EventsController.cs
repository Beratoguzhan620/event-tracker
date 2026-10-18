using EventTracker.Application.Categories;
using EventTracker.Application.Common;
using EventTracker.Application.Events;
using EventTracker.Web.Infrastructure;
using EventTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventTracker.Web.Controllers;

[Route("")]
public sealed class EventsController(
    EventService events,
    CategoryService categories,
    EventClock clock,
    CoverStorage covers,
    Lookups lookups) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? scope, string? q, string? category, int page = 1)
    {
        var query = new EventListQuery
        {
            Scope = scope switch
            {
                "gecmis" => EventScope.Past,
                "tumu" => EventScope.All,
                _ => EventScope.Upcoming,
            },
            Search = q?.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            Page = page,
        };

        return View(new EventIndexViewModel
        {
            Result = await events.ListAsync(query),
            Query = query,
            Categories = await categories.ListAsync(),
            Now = clock.Now,
        });
    }

    [HttpGet("events/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var details = await events.GetAsync(id);
        if (details is null)
            return NotFound();

        return View(new EventDetailsViewModel { Event = details, Now = clock.Now });
    }

    [HttpGet("events/new")]
    public async Task<IActionResult> Create()
    {
        var model = EventFormModel.ForNew(clock.Now);
        model.Categories = await lookups.CategoriesAsync();
        return View("Form", model);
    }

    [HttpPost("events/new")]
    public async Task<IActionResult> Create(EventFormModel model)
    {
        model.Id = null;
        return await SaveAsync(model, input => events.CreateAsync(input));
    }

    [HttpGet("events/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var details = await events.GetAsync(id);
        if (details is null)
            return NotFound();

        var model = EventFormModel.From(details);
        model.Categories = await lookups.CategoriesAsync(model.CategoryId);
        return View("Form", model);
    }

    [HttpPost("events/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, EventFormModel model)
    {
        var details = await events.GetAsync(id);
        if (details is null)
            return NotFound();

        model.Id = id;
        model.ExistingCoverPath = details.CoverImagePath;
        model.ConfirmedCount = details.ConfirmedCount;
        return await SaveAsync(model, input => events.UpdateAsync(id, input));
    }

    [HttpPost("events/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await events.DeleteAsync(id);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        covers.Delete(result.Value);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(EventFormModel model, Func<EventInput, Task<Result<EventSaved>>> save)
    {
        var coverError = CoverStorage.Validate(model.Cover);
        if (coverError is not null)
            ModelState.AddModelError(nameof(EventFormModel.Cover), coverError);

        if (!ModelState.IsValid)
            return await FormAsync(model);

        var newCover = model.Cover is { Length: > 0 } && !model.RemoveCover ? await covers.SaveAsync(model.Cover) : null;

        var result = await save(new EventInput
        {
            Title = model.Title,
            Description = model.Description,
            StartsAt = model.StartsAt,
            EndsAt = model.EndsAt,
            Location = model.Location,
            Capacity = model.Capacity,
            CategoryId = model.CategoryId!.Value,
            NewCoverPath = newCover,
            RemoveCover = model.RemoveCover,
        });

        if (result.Status == ResultStatus.NotFound)
        {
            covers.Delete(newCover);
            return NotFound();
        }

        if (!result.Success)
        {
            covers.Delete(newCover);
            ModelState.AddModelError(EventFormModel.FieldFor(result.Field), result.Message);
            return await FormAsync(model);
        }

        covers.Delete(result.Value!.ReplacedCoverPath);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    private async Task<IActionResult> FormAsync(EventFormModel model)
    {
        model.Categories = await lookups.CategoriesAsync(model.CategoryId);
        return View("Form", model);
    }
}
