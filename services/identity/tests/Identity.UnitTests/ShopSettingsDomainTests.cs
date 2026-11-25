using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-010 domain rules (technical-design §3.1 validation table, §5.2; AC-6, FR-SHOP-005..008).</summary>
public sealed class ShopSettingsDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    private static Shop NewShop() => Shop.Register(Guid.NewGuid(), ShopName.Create("Tiệm Bánh An").Name!, SellerType.Create("individual").Type!, Now);

    private static PickupAddress Address() =>
        PickupAddress.Create("An", "0912345678", "Hà Nội", "Ba Đình", "Phúc Xá", "12 Lê Lợi").Address!;

    private static PayoutDetails Payout() =>
        new(BankName.Create("Vietcombank").Name!, AccountHolder.Create("NGUYEN VAN AN").Holder!, new EncryptedValue("v1", [1, 2, 3]), "4567");

    // ---- description ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void An_empty_description_becomes_null(string? raw)
    {
        var (description, violation) = ShopDescription.Create(raw);

        Assert.Null(description);
        Assert.Null(violation);
    }

    [Theory]
    [InlineData(1000, null)]
    [InlineData(1001, "identity.shop_description_too_long")]
    public void Description_is_at_most_1000_characters(int length, string? code)
    {
        var (description, violation) = ShopDescription.Create(new string('d', length));

        Assert.Equal(code, violation?.Code);
        Assert.Equal(code is null, description is not null);
    }

    [Fact]
    public void Description_keeps_newlines_normalizes_crlf_and_rejects_other_control_characters()
    {
        Assert.Equal("a\nb\nc", ShopDescription.Create(" a\r\nb\nc ").Description!.Value);
        Assert.Equal("identity.shop_description_invalid", ShopDescription.Create("a\u0007b").Violation!.Code);
        Assert.Equal("identity.shop_description_invalid", ShopDescription.Create("a\tb").Violation!.Code);
        Assert.Equal("description", ShopDescription.Create("a\u0007b").Violation!.Field);
    }

    // ---- pickup address ----

    [Fact]
    public void A_valid_address_is_trimmed_collapsed_and_the_phone_is_E164()
    {
        var (address, violations) = PickupAddress.Create("  An   Nguyen ", "0912 345 678", " Hà  Nội", "Ba Đình", "Phúc Xá", "12   Lê Lợi ");

        Assert.Empty(violations);
        Assert.Equal(("An Nguyen", "+84912345678", "Hà Nội", "12 Lê Lợi"), (address!.ContactName, address.Phone, address.Province, address.Street));
    }

    [Theory]
    [InlineData(80, 60, 60, 60, 200, 0)]
    [InlineData(81, 61, 61, 61, 201, 5)]
    public void Address_field_lengths_have_their_boundaries(int contact, int province, int district, int ward, int street, int expectedViolations)
    {
        var (_, violations) = PickupAddress.Create(
            new string('c', contact), "0912345678", new string('p', province), new string('d', district), new string('w', ward), new string('s', street));

        Assert.Equal(expectedViolations, violations.Count);
        Assert.All(violations, v => Assert.Equal("identity.address_field_too_long", v.Code));
    }

    [Fact]
    public void Every_invalid_address_field_is_reported_once_with_its_field_name()
    {
        var (address, violations) = PickupAddress.Create("", "12345", "p\u0007", "  ", null, new string('s', 201));

        Assert.Null(address);
        Assert.Equal(
            [
                ("contactName", "identity.address_field_required"),
                ("phone", "identity.phone_invalid"),
                ("province", "identity.address_field_invalid"),
                ("district", "identity.address_field_required"),
                ("ward", "identity.address_field_required"),
                ("street", "identity.address_field_too_long"),
            ],
            violations.Select(v => (v.Field, v.Code)).ToArray());
    }

    [Fact]
    public void A_blank_phone_is_required_not_invalid()
    {
        var (_, violations) = PickupAddress.Create("An", "  ", "p", "d", "w", "s");

        Assert.Equal(("phone", "identity.address_field_required"), (violations.Single().Field, violations.Single().Code));
    }

    [Fact]
    public void PhoneNumber_reports_the_field_it_was_given_and_defaults_to_phone()
    {
        Assert.Equal("phone", PhoneNumber.Create("1").Violation!.Field);
        Assert.Equal("contactPhone", PhoneNumber.Create("1", "contactPhone").Violation!.Field);
    }

    // ---- bank details ----

    [Theory]
    [InlineData(1, "identity.bank_name_invalid")]
    [InlineData(2, null)]
    [InlineData(80, null)]
    [InlineData(81, "identity.bank_name_too_long")]
    public void Bank_name_is_2_to_80_characters(int length, string? code)
    {
        Assert.Equal(code, BankName.Create(new string('b', length)).Violation?.Code);
    }

    [Theory]
    [InlineData(1, "identity.bank_account_holder_invalid")]
    [InlineData(2, null)]
    [InlineData(80, null)]
    [InlineData(81, "identity.bank_account_holder_too_long")]
    public void Account_holder_is_2_to_80_characters(int length, string? code)
    {
        Assert.Equal(code, AccountHolder.Create(new string('h', length)).Violation?.Code);
    }

    [Fact]
    public void Blank_and_control_character_bank_text_is_rejected()
    {
        Assert.Equal("identity.bank_name_required", BankName.Create(" ").Violation!.Code);
        Assert.Equal("identity.bank_name_invalid", BankName.Create("Viet\u0007com").Violation!.Code);
        Assert.Equal("identity.bank_account_holder_required", AccountHolder.Create(null).Violation!.Code);
        Assert.Equal("identity.bank_account_holder_invalid", AccountHolder.Create("A\nB").Violation!.Code);
        Assert.Equal("bankName", BankName.Create("").Violation!.Field);
        Assert.Equal("accountHolder", AccountHolder.Create("").Violation!.Field);
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    [InlineData("1234567890123456789", true)]
    [InlineData("12345678901234567890", false)]
    [InlineData("12ab", false)]
    [InlineData("0071 0012-34567", true)]
    [InlineData("١٢٣٤٥٦٧", false)]
    public void Account_number_is_6_to_19_digits_after_removing_spaces_and_hyphens(string raw, bool valid)
    {
        var (number, violation) = BankAccountNumber.Create(raw);

        Assert.Equal(valid, number is not null);
        Assert.Equal(valid ? null : "identity.bank_account_number_invalid", violation?.Code);
    }

    [Fact]
    public void A_blank_account_number_is_required_and_the_digits_are_cleaned()
    {
        Assert.Equal("identity.bank_account_number_required", BankAccountNumber.Create("  ").Violation!.Code);
        Assert.Equal("accountNumber", BankAccountNumber.Create("").Violation!.Field);
        Assert.Equal("0071001234567", BankAccountNumber.Create("0071 0012 34567").Number!.Digits);
    }

    [Fact]
    public void The_account_number_never_prints_its_digits()
    {
        var number = BankAccountNumber.Create("0071001234567").Number!;

        Assert.Equal("4567", number.Last4);
        Assert.Equal("••••4567", number.ToString());
        Assert.DoesNotContain("0071001234567", $"{number}");
        Assert.DoesNotContain("0071", number.ToString());
    }

    // ---- Shop behavior ----

    [Fact]
    public void A_new_shop_has_version_1_no_settings_and_is_not_ready()
    {
        var shop = NewShop();

        Assert.Equal(1, shop.ProfileVersion);
        Assert.Null(shop.Description);
        Assert.Null(shop.PickupAddress);
        Assert.Null(shop.Payout);
        Assert.False(shop.SetupComplete);
    }

    [Fact]
    public void UpdateProfile_raises_the_version_only_when_something_changed()
    {
        var shop = NewShop();
        var later = Now.AddMinutes(5);

        Assert.True(shop.UpdateProfile(ShopName.Create("Tiệm Bánh An 2").Name!, ShopDescription.Create("Bánh mì").Description, later));
        Assert.Equal((2, "Tiệm Bánh An 2", "Bánh mì", later), (shop.ProfileVersion, shop.Name, shop.Description, shop.UpdatedAt));

        Assert.False(shop.UpdateProfile(ShopName.Create("Tiệm Bánh An 2").Name!, ShopDescription.Create("Bánh mì").Description, later.AddMinutes(1)));
        Assert.Equal((2, later), (shop.ProfileVersion, shop.UpdatedAt));

        Assert.True(shop.UpdateProfile(ShopName.Create("Tiệm Bánh An 2").Name!, null, later));
        Assert.Equal((3, (string?)null), (shop.ProfileVersion, shop.Description));
    }

    [Fact]
    public void A_change_of_case_only_is_a_change_and_updates_the_normalized_name()
    {
        var shop = NewShop();

        Assert.True(shop.UpdateProfile(ShopName.Create("TIỆM BÁNH AN").Name!, null, Now));
        Assert.Equal(("TIỆM BÁNH AN", "tiệm bánh an"), (shop.Name, shop.NormalizedName));
    }

    [Fact]
    public void The_readiness_flip_is_reported_once_whichever_section_comes_last()
    {
        var shop = NewShop();

        Assert.False(shop.SetPickupAddress(Address(), Now));
        Assert.False(shop.SetupComplete);
        Assert.Equal(1, shop.ProfileVersion);

        Assert.True(shop.SetPayout(Payout(), Now));
        Assert.True(shop.SetupComplete);
        Assert.Equal(2, shop.ProfileVersion);

        Assert.False(shop.SetPayout(Payout(), Now));
        Assert.False(shop.SetPickupAddress(Address(), Now));
        Assert.Equal(2, shop.ProfileVersion);

        var other = NewShop();
        Assert.False(other.SetPayout(Payout(), Now));
        Assert.True(other.SetPickupAddress(Address(), Now));
    }
}
