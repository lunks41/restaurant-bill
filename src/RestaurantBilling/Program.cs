using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Data.Persistence;
using Data.Seeding;
using ExceptionHandling;
using Extensions;
using Services.Jobs;
using RestaurantBilling.Hubs;
using Serilog;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPrinting();

builder.Services.AddAuthentication()
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "RestaurantBilling";
    var audience = builder.Configuration["Jwt:Audience"] ?? "RestaurantBillingClients";
    var key = builder.Configuration["Jwt:Key"] ?? "THIS_IS_DEV_KEY_CHANGE_ME_1234567890";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;

    //options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
    //    ? CookieSecurePolicy.SameAsRequest
    //    : CookieSecurePolicy.Always;
    // Keep login cookie usable in both HTTP (local/test tunnels)
    // and HTTPS environments.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

var mvcBuilder = builder.Services.AddControllersWithViews()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var razorPagesBuilder = builder.Services.AddRazorPages();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
    razorPagesBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddSignalR();
builder.Services.AddHangfireServer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var appCulture = new CultureInfo("en-GB");
appCulture.DateTimeFormat.ShortDatePattern = "dd-MMM-yyyy";
appCulture.DateTimeFormat.LongDatePattern = "dd-MMM-yyyy";
var supportedCultures = new[] { appCulture };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-GB");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});
CultureInfo.DefaultThreadCurrentCulture = supportedCultures[0];
CultureInfo.DefaultThreadCurrentUICulture = supportedCultures[0];
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api-fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 20;
    });
});

var app = builder.Build();
var perishableExpiryCron = "15 2 * * *";

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Auto migration/ensure is intentionally disabled during app startup.
    // Run schema changes manually using dotnet ef commands when needed.
    //var migrations = db.Database.GetMigrations();
    //if (migrations.Any())
    //{
    //    await db.Database.MigrateAsync();
    //}
    //else
    //{
    //    await db.Database.EnsureCreatedAsync();
    //}
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser<int>>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<int>>>();
    //await DbSeeder.SeedAsync(db, userManager, roleManager);

    var closingTimeSetting = await db.RestaurantSettings
        .AsNoTracking()
        .Where(x => x.SettingKey == "ClosingTime")
        .Select(x => x.SettingValue)
        .FirstOrDefaultAsync();
    var parsed = TimeOnly.TryParse(closingTimeSetting, out var closingTime)
        ? closingTime
        : new TimeOnly(2, 0);
    var runAt = parsed.AddMinutes(15);
    perishableExpiryCron = $"{runAt.Minute} {runAt.Hour} * * *";
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<OutboxProcessorJob>("outbox-processor", j => j.Execute(default), "*/2 * * * *");
RecurringJob.AddOrUpdate<PerishableStockExpiryJob>("perishable-expiry", j => j.Execute(default), perishableExpiryCron);

app.MapControllers().RequireRateLimiting("api-fixed");
app.MapRazorPages();
app.MapHub<KdsHub>("/hubs/kds");
app.MapHub<TableHub>("/hubs/table");
app.MapHub<AlertHub>("/hubs/alert");
app.MapDefaultControllerRoute();

app.Run();

