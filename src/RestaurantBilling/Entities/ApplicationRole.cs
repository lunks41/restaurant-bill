using Microsoft.AspNetCore.Identity;

namespace Entities;

public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }
}
