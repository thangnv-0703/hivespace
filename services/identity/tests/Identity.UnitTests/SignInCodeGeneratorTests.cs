using System.Security.Cryptography;
using System.Text;
using Identity.Infrastructure.Security;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-006 T4: the code generator (NFR-SEC-016, D-7).</summary>
public sealed class SignInCodeGeneratorTests
{
    private readonly SignInCodeGenerator _generator = new();

    [Fact]
    public void New_codes_are_six_digits()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.Matches("^[0-9]{6}$", _generator.NewCode());
        }
    }

    [Fact]
    public void Small_values_are_zero_padded_and_the_whole_range_is_asked_for()
    {
        (int From, int To)? asked = null;
        var generator = new SignInCodeGenerator((from, to) =>
        {
            asked = (from, to);
            return 42;
        });

        Assert.Equal("000042", generator.NewCode());
        Assert.Equal((0, 1_000_000), asked);
    }

    [Fact]
    public void The_default_random_source_is_the_csprng()
    {
        var source = _generator.RandomSource;

        Assert.Equal(typeof(RandomNumberGenerator), source.Method.DeclaringType);
        Assert.Equal(nameof(RandomNumberGenerator.GetInt32), source.Method.Name);
    }

    [Fact]
    public void The_hash_is_sha256_of_the_id_then_the_code()
    {
        var id = Guid.CreateVersion7();
        var expected = SHA256.HashData([.. id.ToByteArray(bigEndian: true), .. Encoding.UTF8.GetBytes("123456")]);

        Assert.Equal(expected, _generator.HashCode(id, "123456"));
    }

    [Fact]
    public void The_same_code_hashes_differently_under_two_challenge_ids() =>
        Assert.NotEqual(_generator.HashCode(Guid.CreateVersion7(), "123456"), _generator.HashCode(Guid.CreateVersion7(), "123456"));

    [Fact]
    public void Matches_accepts_the_code_and_rejects_a_wrong_code_or_another_challenge()
    {
        var id = Guid.CreateVersion7();
        var hash = _generator.HashCode(id, "123456");

        Assert.True(_generator.Matches(id, "123456", hash));
        Assert.False(_generator.Matches(id, "123457", hash));
        Assert.False(_generator.Matches(Guid.CreateVersion7(), "123456", hash));
        Assert.False(_generator.Matches(id, "123456", [1, 2, 3]));
    }

    [Fact]
    public void The_email_hash_is_sha256_of_the_normalized_address() =>
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("an@example.com")), _generator.HashEmail("an@example.com"));
}
