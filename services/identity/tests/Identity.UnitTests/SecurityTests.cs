using System.Text;
using Identity.Infrastructure.Security;
using Xunit;

namespace Identity.UnitTests;

public sealed class SecurityTests
{
    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Ac1_hash_is_an_argon2id_phc_string_without_the_password()
    {
        var hash = _hasher.Hash("hello1234");

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain("hello1234", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_password_hashes_differently_each_time() =>
        Assert.NotEqual(_hasher.Hash("hello1234"), _hasher.Hash("hello1234"));

    [Fact]
    public void Verify_accepts_the_right_password_and_rejects_others()
    {
        var hash = _hasher.Hash("hello1234");

        Assert.True(_hasher.Verify(hash, "hello1234"));
        Assert.False(_hasher.Verify(hash, "hello12345"));
    }

    [Fact]
    public void Ac13_no_hash_never_verifies_but_still_does_the_work()
    {
        Assert.False(_hasher.Verify(null, "hello1234"));
        Assert.False(_hasher.Verify("not-a-phc-string", "hello1234"));
    }

    [Fact]
    public void Token_is_base64url_and_only_its_sha256_is_kept()
    {
        var generator = new Sha256TokenGenerator();

        var generated = generator.Generate();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", generated.Token);
        Assert.Equal(32, generated.Hash.Length);
        Assert.Equal(generated.Hash, generator.Hash(generated.Token));
        Assert.NotEqual(Encoding.UTF8.GetBytes(generated.Token), generated.Hash);
        Assert.NotEqual(generated.Token, generator.Generate().Token);
    }
}
