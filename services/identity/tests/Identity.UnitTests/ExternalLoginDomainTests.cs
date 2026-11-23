using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-004 T2: the domain half of Google sign-in (FR-AUTH-020).</summary>
public sealed class ExternalLoginDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1_RegisterExternal_gives_a_verified_buyer()
    {
        var user = User.RegisterExternal(Email.Create("An@Gmail.com").Email!, DisplayName.Create("An Nguyen").Name!, "en", Now);

        Assert.True(user.IsEmailVerified);
        Assert.Equal(Now, user.EmailVerifiedAt);
        Assert.Equal([Role.Buyer], user.Roles.Select(r => r.Role));
        Assert.Equal("An@Gmail.com", user.EmailAddress);
        Assert.Equal("an@gmail.com", user.NormalizedEmail);
        Assert.Equal("An Nguyen", user.DisplayName);
        Assert.Equal("en", user.Language);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Register_is_unchanged_and_starts_unverified()
    {
        var user = User.Register(Email.Create("an@example.com").Email!, DisplayName.Create("An").Name!, "vi", Now);

        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Equal([Role.Buyer], user.Roles.Select(r => r.Role));
    }

    [Fact]
    public void Link_records_the_provider_subject_and_time()
    {
        var userId = Guid.CreateVersion7();

        var link = ExternalLogin.Link(userId, "google", "1234567890", Now);

        Assert.Equal(userId, link.UserId);
        Assert.Equal("google", link.Provider);
        Assert.Equal("1234567890", link.Subject);
        Assert.Equal(Now, link.LinkedAt);
        Assert.NotEqual(Guid.Empty, link.Id);
    }
}
