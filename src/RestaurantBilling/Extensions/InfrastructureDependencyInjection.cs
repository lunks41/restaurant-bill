using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IServices;
using Data.Persistence;
using Services;
using Services.Jobs;
using Repository;

namespace Extensions;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services
            .AddIdentity<IdentityUser<int>, IdentityRole<int>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddHangfire(cfg => cfg
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(15),
                UseRecommendedIsolationLevel = true
            }));

        services.AddScoped<INumberGeneratorService, NumberGeneratorService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<OutboxProcessorJob>();
        services.AddScoped<CashPaymentProvider>();

        services.AddHttpClient<RazorpayUpiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.razorpay.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var phonePeEnv = configuration["PhonePe:Environment"] ?? "sandbox";
        var phonePeBaseUrl = phonePeEnv == "production"
            ? "https://api.phonepe.com/apis/hermes"
            : "https://api-preprod.phonepe.com/apis/pg-sandbox";
        services.AddHttpClient<PhonePeQrProvider>(client =>
        {
            client.BaseAddress = new Uri(phonePeBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<CashPaymentProvider>());
        services.AddScoped<ISalesReportRepository, SalesReportRepository>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IMasterService, MasterService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<UnitOfWork>();
        services.AddPermissionPolicies();

        return services;
    }
}

