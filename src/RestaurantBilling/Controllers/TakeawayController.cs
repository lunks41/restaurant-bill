using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("takeaway")]
public class TakeawayController : Controller
{
    [HttpGet]
    public IActionResult Takeaway()
    {
        ViewBag.UseSelect2 = true;
        return View();
    }
}
