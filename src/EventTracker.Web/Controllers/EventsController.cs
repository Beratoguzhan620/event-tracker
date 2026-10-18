using System.Text;
using EventTracker.Application.Attendance;
using EventTracker.Application.Calendar;
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
    AttendanceService attendance,
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

        return View(DetailsModel(details, new RegistrationFormModel()));
    }

    [HttpPost("events/{id:int}/register")]
    public async Task<IActionResult> Register(int id, [Bind(Prefix = "Registration")] RegistrationFormModel form)
    {
        var details = await events.GetAsync(id);
        if (details is null)
            return NotFound();

        if (ModelState.IsValid)
        {
            var result = await attendance.RegisterAsync(id, new RegistrationInput(form.FullName, form.Email));
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Show", "Registrations", new { token = result.Value!.AccessToken, yeni = 1 });
            }

            ModelState.AddModelError(result.Field is null ? string.Empty : $"Registration.{result.Field}", result.Message);
        }

        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return View("Details", DetailsModel(details, form));
    }

    [HttpGet("events/{id:int}/ics")]
    public async Task<IActionResult> Calendar(int id)
    {
        var details = await events.GetAsync(id);
        if (details is null)
            return NotFound();

        var url = Url.Action(nameof(Details), "Events", new { id }, Request.Scheme);
        var ics = IcsBuilder.Build(CalendarLinks.ToIcs(details, clock, Request.Host.Host, url));
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", CalendarLinks.FileName(details));
    }

    [HttpGet("events/{id:int}/attendees")]
    public async Task<IActionResult> Attendees(int id)
    {
        var list = await attendance.ListAsync(id);
        if (list is null)
            return NotFound();

        var details = await events.GetAsync(id);
        return View(new AttendeesViewModel { List = list, IsPast = details!.EndsAt <= clock.Now });
    }

    [HttpPost("events/{id:int}/attendees/{attendeeId:int}/cancel")]
    public async Task<IActionResult> CancelAttendee(int id, int attendeeId)
    {
        var result = await attendance.CancelByIdAsync(id, attendeeId);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Attendees), new { id });
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

    private EventDetailsViewModel DetailsModel(EventDetails details, RegistrationFormModel form) => new()
    {
        Event = details,
        Now = clock.Now,
        Registration = form,
        GoogleCalendarUrl = CalendarLinks.Google(details, clock, Url.Action(nameof(Details), "Events", new { id = details.Id }, Request.Scheme)),
    };

    private async Task<IActionResult> FormAsync(EventFormModel model)
    {
        model.Categories = await lookups.CategoriesAsync(model.CategoryId);
        return View("Form", model);
    }
}
