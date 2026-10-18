using EventTracker.Application.Calendar;
using EventTracker.Application.Common;
using EventTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventTracker.Web.Controllers;

[Route("calendar")]
public sealed class CalendarController(CalendarService calendar, EventClock clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? view, DateOnly? date)
    {
        var mode = string.Equals(view, "hafta", StringComparison.OrdinalIgnoreCase) ? CalendarView.Week : CalendarView.Month;
        var page = await calendar.GetAsync(mode, date);
        return View(new CalendarViewModel { Page = page, Today = DateOnly.FromDateTime(clock.Today) });
    }
}
