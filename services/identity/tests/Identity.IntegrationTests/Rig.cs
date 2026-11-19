using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Application.Features.Registration;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

public sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
}

/// <summary>The handlers wired to real adapters over one fresh DbContext, like one request scope.</summary>
public sealed class Rig : IAsyncDisposable
{
    private static readonly Argon2idPasswordHasher Hasher = new();
    private static readonly Sha256TokenGenerator Tokens = new();

    private readonly PostgresFixture _postgres;
    private readonly IdentityDbContext _db;

    public Rig(PostgresFixture postgres, FakeClock clock)
    {
        _postgres = postgres;
        _db = postgres.CreateContext();
        Clock = clock;
        var users = new EfUserRepository(_db);
        var outbox = new EfEventOutbox(_db);
        Register = new RegisterUserHandler(users, Hasher, Tokens, outbox, clock);
        Verify = new VerifyEmailHandler(users, Tokens, clock);
        Resend = new ResendVerificationEmailHandler(users, Tokens, outbox, clock);
        SignIn = new SignInHandler(users, Hasher);
    }

    public FakeClock Clock { get; }

    public RegisterUserHandler Register { get; }

    public VerifyEmailHandler Verify { get; }

    public ResendVerificationEmailHandler Resend { get; }

    public SignInHandler SignIn { get; }

    public static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    public static FakeClock NewClock() => new(DateTimeOffset.UtcNow);

    /// <summary>Registers a user and returns its id plus the token that went into the outbox event.</summary>
    public async Task<(Guid UserId, string Email, string Token)> RegisterAsync(string? email = null, string password = "hello1234")
    {
        email ??= NewEmail();
        var result = await Register.HandleAsync(new RegisterUserCommand(email, "An", password, "vi"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return (result.Value, email, await TokenFromOutboxAsync(result.Value));
    }

    public async Task<string> TokenFromOutboxAsync(Guid userId, string eventType = "UserRegistered")
    {
        await using var read = _postgres.CreateContext();
        var rows = await read.Set<OutboxMessage>().Where(m => m.EventType == eventType).OrderByDescending(m => m.OccurredAt).ToListAsync();
        foreach (var row in rows)
        {
            var data = EventEnvelope.FromJson(row.Envelope).Data;
            if (data.GetProperty("userId").GetGuid() == userId)
            {
                return data.GetProperty("verificationToken").GetString()!;
            }
        }

        throw new InvalidOperationException("No outbox event for the user.");
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
