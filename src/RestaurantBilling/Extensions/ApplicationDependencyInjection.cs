using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Services.Billing;
using IServices;

namespace Extensions;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IBillingCalculatorService, BillingCalculatorService>();
        services.AddScoped<IGstCalculatorService, GstCalculatorService>();
        return services;
    }
}

