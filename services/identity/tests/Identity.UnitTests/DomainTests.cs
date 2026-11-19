using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac3_weak_password_lists_every_unmet_rule()
    {
        var codes = PasswordPolicy.Evaluate("abc").Select(v => v.Code).ToList();

        Assert.Contains("identity.password_too_short", codes);
        Assert.Contains("identity.password_needs_digit", codes);
        Assert.DoesNotContain("identity.password_needs_letter", codes);
    }

    [Theory]
    [InlineData("hello1234")]
    [InlineData("12345678a")]
    public void Ac3_policy_compliant_password_is_accepted(string password) =>
        Assert.Empty(PasswordPolicy.Evaluate(password));

    [Fact]
    public void Password_over_128_chars_is_too_long() =>
        Assert.Contains(
            PasswordPolicy.Evaluate("a1" + new string('x', 127)),
            v => v.Code == "identity.password_too_long");

    [Fact]
    public void Password_with_letters_only_needs_a_digit() =>
        Assert.Equal(["identity.password_needs_digit"], PasswordPolicy.Evaluate("abcdefgh").Select(v => v.Code));

    [Theory]
    [InlineData(null, "identity.email_required")]
    [InlineData("  ", "identity.email_required")]
    [InlineData("a@", "identity.email_invalid")]
    [InlineData("no-at-sign.com", "identity.email_invalid")]
    public void Ac4_bad_email_reports_its_field_and_code(string? raw, string code)
    {
        var (email, violation) = Email.Create(raw);

        Assert.Null(email);
        Assert.Equal(new Violation("email", code), violation);
    }

    [Fact]
    public void Email_over_254_chars_is_invalid()
    {
        var (_, violation) = Email.Create(new string('a', 250) + "@b.co");

        Assert.Equal("identity.email_invalid", violation?.Code);
    }

    [Fact]
    public void Email_is_trimmed_and_normalized_case_insensitively()
    {
        var (email, _) = Email.Create("  AN@Example.com ");

        Assert.Equal("AN@Example.com", email!.Value);
        Assert.Equal("an@example.com", email.Normalized);
        Assert.Equal(email.Normalized, Email.Normalize("an@EXAMPLE.com"));
    }

    [Fact]
    public void Ac4_empty_display_name_is_required_and_61_chars_is_too_long()
    {
        Assert.Equal("identity.display_name_required", DisplayName.Create("  ").Violation?.Code);
        Assert.Equal("identity.display_name_too_long", DisplayName.Create(new string('n', 61)).Violation?.Code);
        Assert.Equal("An", DisplayName.Create(" An ").Name!.Value);
    }

    [Fact]
    public void Ac1_new_user_is_unverified_with_the_buyer_role()
    {
        var user = NewUser("en");

        Assert.False(user.IsEmailVerified);
        Assert.Equal([Role.Buyer], user.Roles.Select(r => r.Role));
        Assert.Equal("en", user.Language);
        Assert.Equal(7, user.Id.Version);
    }

    [Fact]
    public void Unknown_language_falls_back_to_vi() =>
        Assert.Equal("vi", NewUser("fr").Language);

    [Fact]
    public void Verifying_twice_keeps_the_first_timestamp()
    {
        var user = NewUser("vi");
        user.VerifyEmail(Now);
        user.VerifyEmail(Now.AddHours(1));

        Assert.Equal(Now, user.EmailVerifiedAt);
    }

    [Fact]
    public void Token_is_valid_until_24_hours_then_expired()
    {
        var token = VerificationToken.Issue(Guid.NewGuid(), [1], Now);

        Assert.Equal(TokenCheck.Valid, token.Check(Now.AddHours(23)));
        Assert.Equal(TokenCheck.Expired, token.Check(Now.AddHours(24)));
    }

    [Fact]
    public void Used_token_reports_already_used_even_after_expiry()
    {
        var token = VerificationToken.Issue(Guid.NewGuid(), [1], Now);
        token.MarkUsed(Now.AddMinutes(1));

        Assert.Equal(TokenCheck.AlreadyUsed, token.Check(Now.AddMinutes(2)));
        Assert.Equal(TokenCheck.AlreadyUsed, token.Check(Now.AddDays(3)));
    }

    [Fact]
    public void Invalidated_token_reads_as_expired()
    {
        var token = VerificationToken.Issue(Guid.NewGuid(), [1], Now);
        token.Invalidate(Now.AddMinutes(1));

        Assert.Equal(TokenCheck.Expired, token.Check(Now.AddMinutes(2)));
    }

    [Fact]
    public void Invalidating_a_used_token_does_not_change_it()
    {
        var token = VerificationToken.Issue(Guid.NewGuid(), [1], Now);
        token.MarkUsed(Now);
        token.Invalidate(Now.AddMinutes(1));

        Assert.Null(token.InvalidatedAt);
        Assert.Equal(TokenCheck.AlreadyUsed, token.Check(Now.AddMinutes(2)));
    }

    private static User NewUser(string language)
    {
        var email = Email.Create("an@example.com").Email!;
        var name = DisplayName.Create("An").Name!;
        return User.Register(email, name, language, Now);
    }
}
