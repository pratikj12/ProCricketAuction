using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProCricketAuction.Api.Endpoints;
using ProCricketAuction.Api.Security;
using ProCricketAuction.Api.Services;
using ProCricketAuction.Domain.Entities;
using ProCricketAuction.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

var databaseConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    switch (databaseProvider.Trim().ToLowerInvariant())
    {
        case "sqlite":
            options.UseSqlite(databaseConnection);
            break;
        case "sqlserver":
            options.UseSqlServer(databaseConnection);
            break;
        default:
            throw new InvalidOperationException("Database:Provider must be either Sqlite or SqlServer.");
    }
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ProCricket.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "ProCricket.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SuperAdministratorAuthorizationHandler.PolicyName, policy =>
        policy.AddRequirements(new SuperAdministratorRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, SuperAdministratorAuthorizationHandler>();
builder.Services.AddValidation();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IAuctionNotifier, SignalRAuctionNotifier>();
builder.Services.AddHostedService<AuctionDeadlineWorker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await BusinessSchemaInitializer.InitializeAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await using var scope = app.Services.CreateAsyncScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Organizer", "Auctioneer", "Captain", "Spectator" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create the {roleName} role: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapAuctionEndpoints();
app.MapHub<ProCricketAuction.Api.Hubs.AuctionHub>("/hubs/auction");
app.MapHub<ProCricketAuction.Api.Hubs.PublicAuctionHub>("/hubs/public-auction")
    .AllowAnonymous();
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithSummary("Check API health");
app.Run();
