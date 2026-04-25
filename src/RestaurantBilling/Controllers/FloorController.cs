using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("floor")]
public class FloorController : Controller
{
    [HttpGet]
    public IActionResult Floor()
    {
        ViewBag.UseSelect2 = true;
        return View();
    }
}
