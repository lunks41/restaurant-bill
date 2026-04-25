using Microsoft.Extensions.DependencyInjection;
using IServices;
using Services;

namespace Extensions;

public static class PrintingDependencyInjection
{
    public static IServiceCollection AddPrinting(this IServiceCollection services)
    {
        services.AddScoped<IPrintService, PrintService>();
        return services;
    }
}

