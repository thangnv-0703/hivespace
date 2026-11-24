using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-009 domain rules (technical-design §3.1, §5.2): shop name, seller type, role grant, audit entry.</summary>
public sealed class ShopDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3)]
    [InlineData(60)]
    public void Names_of_3_to_60_characters_are_accepted(int length)
    {
        var (name, violation) = ShopName.Create(new string('a', length));

        Assert.Null(violation);
        Assert.Equal(length, name!.Value.Length);
    }

    [Theory]
    [InlineData(2, "identity.shop_name_too_short")]
    [InlineData(61, "identity.shop_name_too_long")]
    public void Names_outside_3_to_60_characters_are_rejected(int length, string code)
    {
        var (name, violation) = ShopName.Create(new string('a', length));

        Assert.Null(name);
        Assert.Equal("name", violation!.Field);
        Assert.Equal(code, violation.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_required(string? raw)
    {
        var (_, violation) = ShopName.Create(raw);

        Assert.Equal("identity.shop_name_required", violation!.Code);
    }

    [Fact]
    public void Spaces_are_trimmed_and_collapsed()
    {
        var (name, violation) = ShopName.Create("  Tiệm   Bánh  An  ");

        Assert.Null(violation);
        Assert.Equal("Tiệm Bánh An", name!.Value);
    }

    [Fact]
    public void Length_is_counted_after_trimming_and_collapsing()
    {
        var (name, violation) = ShopName.Create("  " + new string('a', 60) + "  ");

        Assert.Null(violation);
        Assert.Equal(60, name!.Value.Length);
    }

    [Theory]
    [InlineData("Shop\u0000Name")]
    [InlineData("Shop\u0007Name")]
    [InlineData("Shop\u0085Name")]
    [InlineData("Shop\tName")]
    [InlineData("Shop\nName")]
    public void Control_characters_are_rejected(string raw)
    {
        var (name, violation) = ShopName.Create(raw);

        Assert.Null(name);
        Assert.Equal("identity.shop_name_invalid", violation!.Code);
    }

    [Fact]
    public void Names_that_differ_only_by_case_and_spacing_normalize_equal()
    {
        var a = ShopName.Create("Tiệm Bánh An").Name!;
        var b = ShopName.Create("tiệm  bánh an ").Name!;

        Assert.Equal(a.Normalized, b.Normalized);
    }

    [Fact]
    public void Composed_and_decomposed_accents_normalize_equal()
    {
        var composed = ShopName.Create("Tiệm Bánh An").Name!;
        var decomposed = ShopName.Create("Tiệm Bánh An".Normalize(System.Text.NormalizationForm.FormD)).Name!;

        Assert.Equal(composed.Normalized, decomposed.Normalized);
    }

    [Fact]
    public void A_name_without_the_accent_is_a_different_name()
    {
        var accented = ShopName.Create("Tiệm Bánh An").Name!;
        var plain = ShopName.Create("Tiem Banh An").Name!;

        Assert.NotEqual(accented.Normalized, plain.Normalized);
    }

    [Theory]
    [InlineData("individual")]
    [InlineData("business")]
    public void Both_seller_types_are_accepted(string raw)
    {
        var (type, violation) = SellerType.Create(raw);

        Assert.Null(violation);
        Assert.Equal(raw, type!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("Individual")]
    public void Any_other_seller_type_is_rejected(string? raw)
    {
        var (type, violation) = SellerType.Create(raw);

        Assert.Null(type);
        Assert.Equal("sellerType", violation!.Field);
        Assert.Equal("identity.seller_type_invalid", violation.Code);
    }

    [Fact]
    public void Registering_a_shop_keeps_the_owner_and_both_normalized_forms()
    {
        var owner = Guid.NewGuid();
        var name = ShopName.Create("Tiệm  Bánh An").Name!;
        var type = SellerType.Create("business").Type!;

        var shop = Shop.Register(owner, name, type, Now);

        Assert.NotEqual(Guid.Empty, shop.Id);
        Assert.Equal(owner, shop.OwnerUserId);
        Assert.Equal("Tiệm Bánh An", shop.Name);
        Assert.Equal("tiệm bánh an", shop.NormalizedName);
        Assert.Equal("business", shop.SellerType);
        Assert.Equal(Now, shop.CreatedAt);
    }

    [Fact]
    public void Granting_Seller_keeps_Buyer_and_a_second_grant_adds_nothing()
    {
        var user = NewUser();

        Assert.True(user.GrantRole(Role.Seller, Now));
        Assert.False(user.GrantRole(Role.Seller, Now.AddMinutes(1)));

        Assert.Equal(2, user.Roles.Count);
        Assert.Contains(user.Roles, r => r.Role == Role.Buyer);
        Assert.Single(user.Roles, r => r.Role == Role.Seller);
    }

    [Fact]
    public void The_seller_grant_audit_entry_has_a_user_and_no_method()
    {
        var userId = Guid.NewGuid();

        var entry = AuditEntry.SellerRoleGranted(userId, Now);

        Assert.Equal("seller_role_granted", entry.Type);
        Assert.Equal(userId, entry.UserId);
        Assert.Null(entry.Method);
        Assert.Null(entry.Reason);
    }

    private static User NewUser()
    {
        var (email, _) = Email.Create("an@example.com");
        var (displayName, _) = DisplayName.Create("An");
        return User.Register(email!, displayName!, "vi", Now);
    }
}
