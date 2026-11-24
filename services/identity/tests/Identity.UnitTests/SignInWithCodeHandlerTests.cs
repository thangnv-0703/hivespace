using Identity.Application.Abstractions;
using Identity.Application.Features.CodeSignIn;
using Identity.Application.Features.Lockout;
using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-006 T7: a locked account's code is never compared (AC-8, FR-AUTH-035).</summary>
public sealed class SignInWithCodeHandlerTests
{
    [Fact]
    public async Task Ac8_matches_is_not_called_for_a_locked_account_and_the_challenge_count_does_not_change()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));
        var now = clock.GetUtcNow();
        var user = User.Register(Email.Create("an@example.com").Email!, DisplayName.Create("An").Name!, "vi", now);
        var lockout = SignInLockout.Start(user.Id, now);
        for (var i = 1; i < LockoutPolicy.Threshold; i++)
        {
            lockout.RecordFailure(now);
        }

        var challenge = SignInCodeChallenge.Issue(Guid.CreateVersion7(now), [1], user.Id, [2], now);
        var store = new Store(lockout, challenge);
        var codes = new SpyCodes();
        var handler = new SignInWithCodeHandler(store, store, codes, store, store, new LockoutRecorder(store, store, store, clock), clock);

        var result = await handler.HandleAsync(new SignInWithCodeCommand(challenge.Id.ToString(), "123456", null), CancellationToken.None);

        Assert.Equal("identity.account_locked", result.Failure?.Code);
        Assert.Equal(0, codes.Comparisons);
        Assert.Equal(0, challenge.FailedCount);
        Assert.False(challenge.IsEnded);
        var entry = Assert.Single(store.Audit);
        Assert.Equal((AuditEntry.SignInFailedType, AuditMethod.Code, "identity.account_locked", user.Id), (entry.Type, entry.Method, entry.Reason, entry.UserId));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData("١٢٣٤٥٦")]
    [InlineData(null)]
    public async Task Ac11_a_code_that_is_not_six_ascii_digits_is_a_validation_failure_with_nothing_read(string? code)
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var store = new Store(null, null);
        var codes = new SpyCodes();
        var handler = new SignInWithCodeHandler(store, store, codes, store, store, new LockoutRecorder(store, store, store, clock), clock);

        var result = await handler.HandleAsync(new SignInWithCodeCommand(Guid.NewGuid().ToString(), code, null), CancellationToken.None);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        Assert.Equal(("code", "identity.sign_in_code_invalid"), (result.Failure!.Violations![0].Field, result.Failure.Violations[0].Code));
        Assert.Equal(0, codes.Comparisons);
        Assert.Empty(store.Audit);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SpyCodes : ISignInCodeGenerator
    {
        public int Comparisons { get; private set; }

        public bool Matches(Guid challengeId, string code, byte[] codeHash)
        {
            Comparisons++;
            return true;
        }

        public string NewCode() => throw new NotSupportedException();

        public byte[] HashCode(Guid challengeId, string code) => throw new NotSupportedException();

        public byte[] HashEmail(string normalizedEmail) => throw new NotSupportedException();
    }

    /// <summary>Only what code sign-in touches before the comparison; everything else is a bug in the test.</summary>
    private sealed class Store(SignInLockout? lockout, SignInCodeChallenge? challenge) : IUserRepository, ILockoutStore, IAuditLog, ISignInChallengeStore
    {
        public List<AuditEntry> Audit { get; } = [];

        public Task<SignInCodeChallenge?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(challenge?.Id == id ? challenge : null);

        Task<SignInLockout?> ILockoutStore.FindAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(lockout);

        public void Add(AuditEntry entry) => Audit.Add(entry);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SignInCodeChallenge?> FindNewestAsync(byte[] emailHash, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SignInCodeChallenge>> FindOpenAsync(byte[] emailHash, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(SignInCodeChallenge newChallenge) => throw new NotSupportedException();

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Credential?> FindCredentialAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<VerificationToken?> FindTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<VerificationToken>> FindPendingTokensAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(User user, Credential credential, VerificationToken token) => throw new NotSupportedException();

        public void Add(User user) => throw new NotSupportedException();

        public Task<ExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExternalLogin?> FindExternalLoginByUserAsync(Guid userId, string provider, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void AddExternalLogin(ExternalLogin link) => throw new NotSupportedException();

        public void RemoveCredential(Credential credential) => throw new NotSupportedException();

        public void AddToken(VerificationToken token) => throw new NotSupportedException();

        public void AddCredential(Credential credential) => throw new NotSupportedException();

        public void Add(SignInLockout newLockout) => throw new NotSupportedException();

        public void Remove(SignInLockout old) => throw new NotSupportedException();
    }
}
