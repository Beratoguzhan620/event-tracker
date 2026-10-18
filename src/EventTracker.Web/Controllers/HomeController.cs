using Microsoft.AspNetCore.Mvc;

namespace EventTracker.Web.Controllers;

public sealed class HomeController : Controller
{
    [Route("error")]
    public IActionResult Error() => View("~/Views/Shared/Error.cshtml");

    [Route("not-found")]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Views/Shared/NotFound.cshtml");
    }
}
