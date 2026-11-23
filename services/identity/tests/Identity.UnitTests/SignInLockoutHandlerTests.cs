using Identity.Application.Abstractions;
using Identity.Application.Features.Lockout;
using Identity.Application.Features.Registration;
using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-005 T3: a locked account's password is never checked (AC-2, FR-AUTH-012).</summary>
public sealed class SignInLockoutHandlerTests
{
    [Fact]
    public async Task Ac2_the_hasher_is_not_called_for_a_locked_account()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
        var user = User.Register(Email.Create("an@example.com").Email!, DisplayName.Create("An").Name!, "vi", clock.GetUtcNow());
        var lockout = SignInLockout.Start(user.Id, clock.GetUtcNow());
        for (var i = 1; i < LockoutPolicy.Threshold; i++)
        {
            lockout.RecordFailure(clock.GetUtcNow());
        }

        var store = new Store(user, lockout);
        var hasher = new SpyHasher();
        var handler = new SignInHandler(store, hasher, store, store, new LockoutRecorder(store, store, store, clock), clock);

        var result = await handler.HandleAsync(new SignInCommand("an@example.com", "hello1234", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_locked", result.Failure!.Code);
        Assert.Equal(0, hasher.Verifications);
        var entry = Assert.Single(store.Audit);
        Assert.Equal((AuditEntry.SignInFailedType, AuditMethod.Password, "identity.account_locked"), (entry.Type, entry.Method, entry.Reason));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SpyHasher : IPasswordHasher
    {
        public int Verifications { get; private set; }

        public string Hash(string password) => password;

        public bool Verify(string? hash, string password)
        {
            Verifications++;
            return true;
        }
    }

    /// <summary>Only what sign-in touches; everything else is a bug in the test.</summary>
    private sealed class Store(User user, SignInLockout lockout) : IUserRepository, ILockoutStore, IAuditLog
    {
        public List<AuditEntry> Audit { get; } = [];

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(user);

        public Task<SignInLockout?> FindAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<SignInLockout?>(lockout);

        public void Add(AuditEntry entry) => Audit.Add(entry);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
