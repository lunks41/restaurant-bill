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

    [HttpPost("users-update/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound("User not found.");

        var newUserName = request.UserName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newUserName)) return BadRequest("Username is required.");

        var duplicate = await db.Users.AnyAsync(x => x.Id != id && x.UserName == newUserName, cancellationToken);
        if (duplicate) return Conflict("Username already exists.");

        user.UserName = newUserName;
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var hasher = new PasswordHasher<IdentityUser<int>>();
            user.PasswordHash = hasher.HashPassword(user, request.Password.Trim());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("users-lock-toggle/{id:int}")]
    public async Task<IActionResult> ToggleUserLock(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound("User not found.");

        var shouldLock = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;
        user.LockoutEnabled = true;
        user.LockoutEnd = shouldLock ? DateTimeOffset.UtcNow.AddYears(50) : null;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = shouldLock ? "Locked" : "Unlocked" });
    }

    public sealed record CreateUserRequest(string UserName, string? Email, string? Password);
    public sealed record UpdateUserRequest(string UserName, string? Email, string? Password);
}

