using EventTracker.Application.Attendance;
using EventTracker.Application.Common;
using EventTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventTracker.Web.Controllers;

/// <summary>The personal page behind each attendee's link: status, waiting list position and cancelling.</summary>
[Route("registration/{token:length(32)}")]
public sealed class RegistrationsController(AttendanceService attendance, EventClock clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Show(string token, int? yeni)
    {
        var registration = await attendance.GetByTokenAsync(token);
        if (registration is null)
            return NotFound();

        return View(new RegistrationViewModel { Registration = registration, Now = clock.Now, JustRegistered = yeni == 1 });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string token)
    {
        var result = await attendance.CancelByTokenAsync(token);
        if (result.Status == ResultStatus.NotFound)
            return NotFound();

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success ? "Kaydınız iptal edildi." : result.Message;
        return RedirectToAction(nameof(Show), new { token });
    }
}
