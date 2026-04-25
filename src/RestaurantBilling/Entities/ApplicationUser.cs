using Microsoft.AspNetCore.Identity;

namespace Entities;

public class ApplicationUser : IdentityUser<int>
{
    public string? FullName { get; set; }
    public bool IsEnabled { get; set; } = true;
}
