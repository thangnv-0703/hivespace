using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Application.Features.CodeSignIn;
using Identity.Application.Features.ExternalLogin;
using Identity.Application.Features.Lockout;
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
        services.AddScoped<EfUserRepository>();
        services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<EfUserRepository>());
        services.AddScoped<ILockoutStore>(sp => sp.GetRequiredService<EfUserRepository>());
        services.AddScoped<IAuditLog>(sp => sp.GetRequiredService<EfUserRepository>());
        services.AddScoped<ISignInChallengeStore>(sp => sp.GetRequiredService<EfUserRepository>());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISessionRevoker, OpenIddictSessionRevoker>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IVerificationTokenGenerator, Sha256TokenGenerator>();
        services.AddSingleton<ISignInCodeGenerator, SignInCodeGenerator>();
        // Sent rows still hold the verification token, so they are deleted after 24 hours (R-6, AC-8).
        services.AddOutboxCleanup<IdentityDbContext>(TimeSpan.FromHours(24));

        services.AddHostedService<AuditRetentionSweeper>();
        services.AddHostedService<SignInChallengeSweeper>();

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendVerificationEmailHandler>();
        services.AddScoped<LockoutRecorder>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<RequestSignInCodeHandler>();
        services.AddScoped<SignInWithCodeHandler>();
        services.AddScoped<RecordSignInFailureHandler>();
        services.AddScoped<ChangeMyPasswordHandler>();
        services.AddScoped<SetMyPasswordHandler>();
        services.AddScoped<ExternalSignInHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<UpdateMyLanguageHandler>();
        return builder;
    }
}
