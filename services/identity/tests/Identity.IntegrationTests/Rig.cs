using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.CodeSignIn;
using Identity.Application.Features.ExternalLogin;
using Identity.Application.Features.Lockout;
using Identity.Application.Features.Password;
using Identity.Application.Features.Profile;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Shops;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>The handlers wired to real adapters over one fresh DbContext, like one request scope.</summary>
public sealed class Rig : IAsyncDisposable
{
    private static readonly Argon2idPasswordHasher Hasher = new();
    private static readonly Sha256TokenGenerator Tokens = new();

    public static readonly SignInCodeGenerator Codes = new();

    private readonly PostgresFixture _postgres;
    private readonly IdentityDbContext _db;
    private readonly EfUserRepository _users;
    private readonly EfEventOutbox<IdentityDbContext> _outbox;

    public Rig(PostgresFixture postgres, FakeTimeProvider clock)
    {
        _postgres = postgres;
        _db = postgres.CreateContext();
        Clock = clock;
        var users = new EfUserRepository(_db);
        var outbox = new EfEventOutbox<IdentityDbContext>(_db, new MessagingOptions("hivespace-events", "identity"));
        _users = users;
        _outbox = outbox;
        Register = new RegisterUserHandler(users, Hasher, Tokens, outbox, clock);
        Verify = new VerifyEmailHandler(users, Tokens, clock);
        Resend = new ResendVerificationEmailHandler(users, Tokens, outbox, clock);
        Recorder = new LockoutRecorder(users, users, users, clock);
        SignIn = new SignInHandler(users, Hasher, users, users, Recorder, clock);
        GetProfile = new GetMyProfileHandler(users);
        UpdateProfile = new UpdateMyProfileHandler(users, outbox, clock);
        UpdateLanguage = new UpdateMyLanguageHandler(users, outbox, clock);
        ChangePassword = new ChangeMyPasswordHandler(users, Hasher, new OpenIddictSessionRevoker(_db), outbox, users, Recorder, clock);
        SetPassword = new SetMyPasswordHandler(users, Hasher, new OpenIddictSessionRevoker(_db), outbox, clock);
        ExternalSignIn = new ExternalSignInHandler(users, new OpenIddictSessionRevoker(_db), outbox, users, clock);
        RequestCode = new RequestSignInCodeHandler(users, users, Codes, outbox, clock);
        SignInWithCode = new SignInWithCodeHandler(users, users, Codes, users, users, Recorder, clock);
        RegisterShop = new RegisterShopHandler(users, users, users, outbox, clock);
        GetMyShop = new GetMyShopHandler(users);
        UpdateShopProfile = new UpdateShopProfileHandler(users, users, outbox, clock);
        SetPickupAddress = new SetPickupAddressHandler(users, users, outbox, clock);
        SetPayout = new SetPayoutHandler(users, users, Protector, outbox, clock);
    }

    /// <summary>The same fixed key as the test host, so a row written by one rig reads back in another.</summary>
    public static readonly Identity.Application.Abstractions.IBankAccountProtector Protector = new AesGcmBankAccountProtector(Microsoft.Extensions.Options.Options.Create(
        new BankEncryptionOptions { ActiveKeyId = "v1", Keys = new() { ["v1"] = IdentityApiFactory.TestBankKey } }));

    public UpdateShopProfileHandler UpdateShopProfile { get; }

    public SetPickupAddressHandler SetPickupAddress { get; }

    public SetPayoutHandler SetPayout { get; }

    public RegisterShopHandler RegisterShop { get; }

    /// <summary>A registration whose name check always passes, as when a parallel request commits the same name just after the check.</summary>
    public RegisterShopHandler RegisterShopBlindToNames() => new(_users, new NameCheckBlindStore(_users), _users, _outbox, Clock);

    public GetMyShopHandler GetMyShop { get; }

    /// <summary>A rename whose name check always passes, as when a parallel request commits the same name just after the check.</summary>
    public UpdateShopProfileHandler UpdateShopProfileBlindToNames() => new(new NameCheckBlindStore(_users), _users, _outbox, Clock);

    public SignInWithCodeHandler SignInWithCode { get; }

    public RequestSignInCodeHandler RequestCode { get; }

    public LockoutRecorder Recorder { get; }

    public ExternalSignInHandler ExternalSignIn { get; }

    public SetMyPasswordHandler SetPassword { get; }

    public ChangeMyPasswordHandler ChangePassword { get; }

    public FakeTimeProvider Clock { get; }

    public RegisterUserHandler Register { get; }

    public VerifyEmailHandler Verify { get; }

    public ResendVerificationEmailHandler Resend { get; }

    public SignInHandler SignIn { get; }

    public GetMyProfileHandler GetProfile { get; }

    public UpdateMyProfileHandler UpdateProfile { get; }

    public UpdateMyLanguageHandler UpdateLanguage { get; }

    public static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    public static FakeTimeProvider NewClock() => new(DateTimeOffset.UtcNow);

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

    /// <summary>The code carried by the newest <c>SignInCodeRequested</c> outbox event for the user, or null when there is none.</summary>
    public async Task<string?> CodeFromOutboxAsync(Guid userId)
    {
        await using var read = _postgres.CreateContext();
        var rows = await read.Set<OutboxMessage>().Where(m => m.EventType == "SignInCodeRequested").OrderByDescending(m => m.OccurredAt).ToListAsync();
        return rows
            .Select(r => EventEnvelope.FromJson(r.Envelope).Data)
            .Where(d => d.GetProperty("userId").GetGuid() == userId)
            .Select(d => d.GetProperty("code").GetString())
            .FirstOrDefault();
    }

    /// <summary>Requests a code and returns the issued challenge; fails the test when it was not issued.</summary>
    public async Task<SignInCodeRequestOutcome.Issued> IssueCodeAsync(string email)
    {
        var result = await RequestCode.HandleAsync(new RequestSignInCodeCommand(email), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return Assert.IsType<SignInCodeRequestOutcome.Issued>(result.Value);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}

/// <summary>Delegates to the real store but reports every name as free, so the unique index is what rejects a duplicate.</summary>
internal sealed class NameCheckBlindStore(Identity.Application.Abstractions.IShopStore inner) : Identity.Application.Abstractions.IShopStore
{
    public Task<Identity.Domain.Shop?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) => inner.FindByOwnerAsync(ownerUserId, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult(false);

    public void Add(Identity.Domain.Shop shop) => inner.Add(shop);
}
