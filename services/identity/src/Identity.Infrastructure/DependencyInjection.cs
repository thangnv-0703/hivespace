using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Application.Features.Password;
using Identity.Application.Features.Profile;
using Identity.Application.Features.Registration;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Connection names match the AppHost resources (apphost/Resources).</summary>
    public const string DatabaseConnection = "identitydb";
    public const string MessagingConnection = "messaging";
    public const string EventsTopic = "hivespace-events";

    public static IHostApplicationBuilder AddIdentityInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<IdentityDbContext>(
            DatabaseConnection,
            configureDbContextOptions: options => options.UseOpenIddict());
        builder.AddEventBus<IdentityDbContext>(MessagingConnection, new MessagingOptions(EventsTopic, "identity"));

        var services = builder.Services;
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IEventOutbox, EfEventOutbox>();
        services.AddScoped<ISessionRevoker, OpenIddictSessionRevoker>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IVerificationTokenGenerator, Sha256TokenGenerator>();
        services.AddSingleton<OutboxCleanup>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxCleanup>());

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendVerificationEmailHandler>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<ChangeMyPasswordHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<UpdateMyLanguageHandler>();
        return builder;
    }
}
