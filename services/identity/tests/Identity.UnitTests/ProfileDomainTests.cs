using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-002 domain rules (technical-design §3.1, §5.2): phone, gender, date of birth, language, profile updates.</summary>
public sealed class ProfileDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 19);

    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("0912 345 678", "+84912345678")]
    [InlineData("091-234-5678", "+84912345678")]
    [InlineData("091.234.5678", "+84912345678")]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData("+84 912 345 678", "+84912345678")]
    [InlineData("0331234567", "+84331234567")]
    [InlineData("0281234567", "+84281234567")]
    [InlineData("+84 28 1234 567", "+84281234567")]
    public void Phone_in_every_accepted_form_is_stored_as_e164(string raw, string expected)
    {
        var (phone, violation) = PhoneNumber.Create(raw);

        Assert.Null(violation);
        Assert.Equal(expected, phone!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("091234567")]     // 8 digits after the 0
    [InlineData("09123456789")]   // 10 digits after the 0
    [InlineData("0112345678")]    // first digit 1 is not a valid prefix
    [InlineData("0412345678")]    // first digit 4
    [InlineData("0612345678")]    // first digit 6
    [InlineData("84912345678")]   // missing the plus
    [InlineData("+85912345678")]  // wrong country code
    [InlineData("+840912345678")] // trunk 0 kept after +84
    [InlineData("09a2345678")]
    public void Phone_in_any_other_form_is_invalid(string raw)
    {
        var (phone, violation) = PhoneNumber.Create(raw);

        Assert.Null(phone);
        Assert.Equal(new Violation("phone", "identity.phone_invalid"), violation);
    }

    [Theory]
    [InlineData("male")]
    [InlineData("female")]
    [InlineData("other")]
    [InlineData("prefer_not_to_say")]
    public void Gender_accepts_the_four_values(string raw) => Assert.Equal(raw, Gender.Create(raw).Gender!.Value);

    [Theory]
    [InlineData("x")]
    [InlineData("")]
    [InlineData("Male")]
    [InlineData(null)]
    public void Gender_rejects_anything_else(string? raw) =>
        Assert.Equal("identity.gender_invalid", Gender.Create(raw).Violation?.Code);

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Language_accepts_vi_and_en(string raw) => Assert.Equal(raw, Language.Create(raw).Language!.Value);

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("VI")]
    [InlineData(null)]
    public void Language_rejects_anything_else(string? raw) =>
        Assert.Equal("identity.language_unsupported", Language.Create(raw).Violation?.Code);

    [Theory]
    [InlineData("2026-11-19")] // today is allowed
    [InlineData("1995-04-30")]
    [InlineData("2006-11-19")] // 120 years before today is allowed: 1906-11-19
    [InlineData("1906-11-19")]
    public void Date_of_birth_from_120_years_ago_up_to_today_is_valid(string raw)
    {
        var (date, violation) = DateOfBirth.Create(raw, Today);

        Assert.Null(violation);
        Assert.Equal(DateOnly.Parse(raw), date!.Value);
    }

    [Theory]
    [InlineData("2026-11-20", "identity.date_of_birth_in_future")]
    [InlineData("2999-01-01", "identity.date_of_birth_in_future")]
    [InlineData("1906-11-18", "identity.date_of_birth_too_old")]
    [InlineData("1800-01-01", "identity.date_of_birth_too_old")]
    [InlineData("1995-02-31", "identity.date_of_birth_invalid")]
    [InlineData("30/04/1995", "identity.date_of_birth_invalid")]
    [InlineData("1995-4-3", "identity.date_of_birth_invalid")]
    [InlineData("", "identity.date_of_birth_invalid")]
    [InlineData(null, "identity.date_of_birth_invalid")]
    public void Date_of_birth_outside_the_rules_names_the_broken_one(string? raw, string code)
    {
        var (date, violation) = DateOfBirth.Create(raw, Today);

        Assert.Null(date);
        Assert.Equal(new Violation("dateOfBirth", code), violation);
    }

    [Fact]
    public void Display_name_allows_80_characters_and_rejects_81()
    {
        Assert.Null(DisplayName.Create(new string('n', 80)).Violation);
        Assert.Equal("identity.display_name_too_long", DisplayName.Create(new string('n', 81)).Violation?.Code);
    }

    [Fact]
    public void UpdateProfile_replaces_all_fields_and_reports_a_display_name_change()
    {
        var user = NewUser();
        var later = Now.AddHours(1);

        var changed = user.UpdateProfile(
            DisplayName.Create("Lan Tran").Name!,
            PhoneNumber.Create("0912 345 678").Phone,
            Gender.Create("female").Gender,
            DateOfBirth.Create("1995-04-30", Today).Date,
            later);

        Assert.Equal(["displayName"], changed);
        Assert.Equal("Lan Tran", user.DisplayName);
        Assert.Equal("+84912345678", user.Phone);
        Assert.Equal("female", user.Gender);
        Assert.Equal(new DateOnly(1995, 4, 30), user.DateOfBirth);
        Assert.Equal(later, user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_with_only_the_phone_changed_reports_nothing_for_the_event()
    {
        var user = NewUser();

        var changed = user.UpdateProfile(DisplayName.Create("An").Name!, PhoneNumber.Create("0912345678").Phone, null, null, Now.AddHours(1));

        Assert.Empty(changed);
        Assert.Equal("+84912345678", user.Phone);
    }

    [Fact]
    public void UpdateProfile_with_nulls_clears_phone_gender_and_date_of_birth()
    {
        var user = NewUser();
        user.UpdateProfile(
            DisplayName.Create("An").Name!, PhoneNumber.Create("0912345678").Phone, Gender.Create("male").Gender, DateOfBirth.Create("1995-04-30", Today).Date, Now);

        user.UpdateProfile(DisplayName.Create("An").Name!, null, null, null, Now.AddHours(1));

        Assert.Null(user.Phone);
        Assert.Null(user.Gender);
        Assert.Null(user.DateOfBirth);
    }

    [Fact]
    public void UpdateProfile_with_the_same_values_changes_nothing_and_keeps_updated_at()
    {
        var user = NewUser();

        var changed = user.UpdateProfile(DisplayName.Create("An").Name!, null, null, null, Now.AddHours(1));

        Assert.Empty(changed);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Fact]
    public void ChangeLanguage_reports_a_change_once_and_is_idempotent()
    {
        var user = NewUser();

        Assert.Equal(["language"], user.ChangeLanguage(Language.Create("en").Language!, Now.AddHours(1)));
        Assert.Equal("en", user.Language);
        Assert.Empty(user.ChangeLanguage(Language.Create("en").Language!, Now.AddHours(2)));
        Assert.Equal(Now.AddHours(1), user.UpdatedAt);
    }

    private static User NewUser() =>
        User.Register(Email.Create("an@example.com").Email!, DisplayName.Create("An").Name!, "vi", Now);
}
