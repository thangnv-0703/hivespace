using HiveSpace.BuildingBlocks.Errors;
using Identity.Api.Endpoints;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddIdentityInfrastructure();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("identitydb", tags: ["ready"]);

// The Identity session (design §3.1): only OpenIddict's authorize endpoint reads it, to know who is signing in.
builder.Services.AddAuthentication(UsersEndpoints.CookieScheme)
    .AddCookie(UsersEndpoints.CookieScheme, o =>
    {
        o.Cookie.Name = UsersEndpoints.CookieScheme;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.Path = "/";
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        // An API: never redirect to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseApiErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    // Local and test only: the AppHost runs migrations on start (architecture §8.2). Cloud uses a deploy step.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

app.MapUsersEndpoints();

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
