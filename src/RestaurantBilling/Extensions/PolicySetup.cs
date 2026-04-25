using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Helper;

namespace Extensions;

public static class PolicySetup
{
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Permissions.CanVoidBill, p => p.RequireRole("Admin", "Manager"));
            options.AddPolicy(Permissions.CanApplyDiscount, p => p.RequireRole("Admin", "Manager", "Cashier"));
            options.AddPolicy(Permissions.CanCloseDay, p => p.RequireRole("Admin", "Manager", "Accountant"));
            options.AddPolicy(Permissions.CanDeleteMaster, p => p.RequireRole("Admin"));
        });

        return services;
    }
}

