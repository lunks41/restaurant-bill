using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace RestaurantBilling.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController(AppDbContext db) : Controller
{
    [HttpGet("users")]
    [HttpGet("/master/user")]
    public IActionResult Users()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("audit")]
    public IActionResult Audit() => View();

    [HttpPost("verify-pin")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPin([FromBody] VerifyPinRequest request, CancellationToken cancellationToken)
    {
        var outletId = await db.Outlets.Select(x => x.OutletId).FirstOrDefaultAsync(cancellationToken);
        var pin = await db.RestaurantSettings
            .Where(x => x.OutletId == outletId && x.SettingKey == "ManagerPin")
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return Ok(new { success = !string.IsNullOrWhiteSpace(pin) && pin == request.Pin });
    }

    public sealed record VerifyPinRequest(string Pin);

    [HttpGet("users-data")]
    public async Task<IActionResult> UsersData(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .OrderBy(x => x.UserName)
            .Select(x => new { x.Id, x.UserName, x.Email, x.LockoutEnabled })
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost("users-create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(x => x.UserName == request.UserName, cancellationToken);
        if (exists) return Conflict("Username already exists.");

        var user = new IdentityUser<int>
        {
            UserName = request.UserName.Trim(),
            Email = request.Email?.Trim()
        };
        var hasher = new PasswordHasher<IdentityUser<int>>();
        user.PasswordHash = hasher.HashPassword(user, string.IsNullOrWhiteSpace(request.Password) ? "Admin@123" : request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created", userId = user.Id });
    }

    public sealed record CreateUserRequest(string UserName, string? Email, string? Password);
}

