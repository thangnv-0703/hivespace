using System.Security.Cryptography;
using System.Text;
using Identity.Domain;
using Identity.Infrastructure;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-010 bank number protection (technical-design §5.2, §5.3; D-1; AC-5; NFR-SEC-008).</summary>
public sealed class BankAccountProtectorTests
{
    private static string Key(byte seed) => Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(seed + i)).ToArray());

    private static AesGcmBankAccountProtector Protector(string active, params (string Id, string Key)[] keys) =>
        new(Options.Create(new BankEncryptionOptions { ActiveKeyId = active, Keys = keys.ToDictionary(k => k.Id, k => k.Key) }));

    private static readonly BankAccountNumber Number = BankAccountNumber.Create("0071001234567").Number!;

    [Fact]
    public void A_number_round_trips_for_the_same_shop_and_the_bytes_hide_the_digits()
    {
        var protector = Protector("v1", ("v1", Key(1)));
        var shop = Guid.NewGuid();

        var sealedValue = protector.Protect(Number, shop);

        Assert.Equal("v1", sealedValue.KeyId);
        Assert.Equal("0071001234567", protector.Unprotect(sealedValue, shop).Digits);
        Assert.DoesNotContain("0071001234567", Encoding.UTF8.GetString(sealedValue.Bytes));
        Assert.DoesNotContain("0071001234567", Encoding.Latin1.GetString(sealedValue.Bytes));
    }

    [Fact]
    public void The_same_number_twice_gives_different_ciphertext()
    {
        var protector = Protector("v1", ("v1", Key(1)));
        var shop = Guid.NewGuid();

        Assert.NotEqual(protector.Protect(Number, shop).Bytes, protector.Protect(Number, shop).Bytes);
    }

    [Fact]
    public void Another_shop_id_cannot_decrypt_the_bytes()
    {
        var protector = Protector("v1", ("v1", Key(1)));
        var sealedValue = protector.Protect(Number, Guid.NewGuid());

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(sealedValue, Guid.NewGuid()));
    }

    [Fact]
    public void A_flipped_byte_fails_authentication()
    {
        var protector = Protector("v1", ("v1", Key(1)));
        var shop = Guid.NewGuid();
        var sealedValue = protector.Protect(Number, shop);
        var tampered = sealedValue.Bytes.ToArray();
        tampered[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(sealedValue with { Bytes = tampered }, shop));
    }

    [Fact]
    public void A_v1_row_still_decrypts_after_v2_becomes_active_and_new_values_use_v2()
    {
        var shop = Guid.NewGuid();
        var old = Protector("v1", ("v1", Key(1))).Protect(Number, shop);
        var rotated = Protector("v2", ("v1", Key(1)), ("v2", Key(9)));

        Assert.Equal("0071001234567", rotated.Unprotect(old, shop).Digits);
        Assert.Equal("v2", rotated.Protect(Number, shop).KeyId);
    }

    [Fact]
    public void A_row_whose_key_is_no_longer_configured_cannot_be_read()
    {
        var shop = Guid.NewGuid();
        var old = Protector("v1", ("v1", Key(1))).Protect(Number, shop);

        Assert.Throws<CryptographicException>(() => Protector("v2", ("v2", Key(9))).Unprotect(old, shop));
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("v1", "", true)]
    [InlineData("v2", "valid", true)]
    [InlineData("v1", "short", true)]
    [InlineData("v1", "valid", false)]
    public void Start_up_validation_rejects_a_missing_or_wrong_sized_key(string active, string keyKind, bool problem)
    {
        var keys = new Dictionary<string, string>();
        if (keyKind == "valid")
        {
            keys["v1"] = Key(1);
        }
        else if (keyKind == "short")
        {
            keys["v1"] = Convert.ToBase64String(new byte[31]);
        }

        var options = new BankEncryptionOptions { ActiveKeyId = active, Keys = keys };

        Assert.Equal(problem, options.Problem() is not null);
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddBankAccountProtection();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("31bytes")]
    public void The_host_refuses_to_start_with_a_missing_or_31_byte_key(string? keyKind)
    {
        var settings = new Dictionary<string, string?> { ["Identity:BankEncryption:ActiveKeyId"] = "v1" };
        if (keyKind is not null)
        {
            settings["Identity:BankEncryption:Keys:v1"] = Convert.ToBase64String(new byte[31]);
        }

        using var provider = Provider(settings);

        // ValidateOnStart registers this validator; the host runs it before it starts serving.
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void The_host_starts_with_a_valid_key_and_resolves_the_protector()
    {
        using var provider = Provider(new Dictionary<string, string?>
        {
            ["Identity:BankEncryption:ActiveKeyId"] = "v1",
            ["Identity:BankEncryption:Keys:v1"] = Key(1),
        });

        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.NotNull(provider.GetRequiredService<Identity.Application.Abstractions.IBankAccountProtector>());
    }
}
