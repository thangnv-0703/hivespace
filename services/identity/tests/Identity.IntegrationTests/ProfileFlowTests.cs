using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.Profile;
using Identity.Application.Features.Registration;
using Identity.Infrastructure.Persistence;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Profile handlers against a real Postgres: AC-1, AC-2, AC-3, AC-4, AC-5, AC-8, AC-11 (handler level).</summary>
public sealed class ProfileFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Event = "UserProfileChanged";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UpdateMyProfileCommand Edit(
        Guid userId,
        string? name = "An",
        string? phone = null,
        string? gender = null,
        string? dateOfBirth = null,
        params string[] unknown) =>
        new(userId, name, phone, gender, dateOfBirth, unknown);

    [Fact]
    public async Task Ac1_get_returns_the_profile_with_a_null_avatar_and_the_registration_language()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();

        var profile = (await rig.GetProfile.HandleAsync(userId, Ct)).Value!;

        Assert.Equal(new ProfileView(userId, email, "An", null, null, null, null, "vi"), profile);
    }

    [Fact]
    public async Task Get_for_an_unknown_user_is_unauthenticated()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.GetProfile.HandleAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal("common.unauthenticated", result.Failure?.Code);
    }

    [Fact]
    public async Task Ac2_update_saves_every_field_and_a_new_read_returns_them_with_the_phone_as_e164()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        var saved = await rig.UpdateProfile.HandleAsync(Edit(userId, "Lan Tran", "0912 345 678", "female", "1995-04-30"), Ct);
        await using var reader = new Rig(postgres, Rig.NewClock());
        var read = (await reader.GetProfile.HandleAsync(userId, Ct)).Value!;

        Assert.True(saved.IsSuccess, saved.Failure?.Code);
        Assert.Equal(("Lan Tran", "+84912345678", "female", "1995-04-30"), (read.DisplayName, read.Phone, read.Gender, read.DateOfBirth));
    }

    [Fact]
    public async Task Ac2_null_clears_phone_gender_and_date_of_birth()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        await rig.UpdateProfile.HandleAsync(Edit(userId, "An", "0912345678", "male", "1990-01-01"), Ct);

        await using var second = new Rig(postgres, Rig.NewClock());
        await second.UpdateProfile.HandleAsync(Edit(userId), Ct);
        await using var reader = new Rig(postgres, Rig.NewClock());
        var read = (await reader.GetProfile.HandleAsync(userId, Ct)).Value!;

        Assert.Equal((null, null, null), (read.Phone, read.Gender, read.DateOfBirth));
    }

    [Fact]
    public async Task Ac3_every_violation_is_reported_together_and_nothing_is_saved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 20, 3, 0, 0, TimeSpan.Zero)); // 10:00 in Vietnam
        await using var rig = new Rig(postgres, clock);
        var (userId, _, _) = await rig.RegisterAsync();

        var result = await rig.UpdateProfile.HandleAsync(Edit(userId, "", "12345", "x", "2026-11-21"), Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        Assert.Equal(
            ["identity.display_name_required", "identity.phone_invalid", "identity.gender_invalid", "identity.date_of_birth_in_future"],
            result.Failure!.Violations!.Select(v => v.Code));
        await using var reader = new Rig(postgres, Rig.NewClock());
        Assert.Equal("An", (await reader.GetProfile.HandleAsync(userId, Ct)).Value!.DisplayName);
    }

    [Fact]
    public async Task Ac3_a_name_of_81_characters_and_a_date_121_years_ago_are_rejected_but_80_characters_is_saved()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        var tooOld = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-121)).ToString("yyyy-MM-dd");

        var longName = await rig.UpdateProfile.HandleAsync(Edit(userId, new string('n', 81)), Ct);
        var oldDate = await rig.UpdateProfile.HandleAsync(Edit(userId, dateOfBirth: tooOld), Ct);
        var okName = await rig.UpdateProfile.HandleAsync(Edit(userId, new string('n', 80)), Ct);

        Assert.Equal("identity.display_name_too_long", longName.Failure!.Violations!.Single().Code);
        Assert.Equal("identity.date_of_birth_too_old", oldDate.Failure!.Violations!.Single().Code);
        Assert.True(okName.IsSuccess);
    }

    [Fact]
    public async Task Today_for_the_date_rule_is_the_date_in_Vietnam()
    {
        // 18:00 UTC on 19 Nov is already 01:00 on 20 Nov in Vietnam, so 20 Nov is "today", not the future.
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero));
        await using var rig = new Rig(postgres, clock);
        var (userId, _, _) = await rig.RegisterAsync();

        var today = await rig.UpdateProfile.HandleAsync(Edit(userId, dateOfBirth: "2026-11-20"), Ct);
        var tomorrow = await rig.UpdateProfile.HandleAsync(Edit(userId, dateOfBirth: "2026-11-21"), Ct);

        Assert.True(today.IsSuccess, today.Failure?.Code);
        Assert.Equal("identity.date_of_birth_in_future", tomorrow.Failure!.Violations!.Single().Code);
    }

    [Fact]
    public async Task Ac4_an_email_key_is_reported_as_read_only_alongside_other_violations_and_the_email_is_unchanged()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();

        var result = await rig.UpdateProfile.HandleAsync(Edit(userId, "", unknown: ["email", "role"]), Ct);

        Assert.Equal(
            [new("email", "identity.email_read_only"), new("role", "common.validation_failed"), new("displayName", "identity.display_name_required")],
            result.Failure!.Violations);
        await using var reader = new Rig(postgres, Rig.NewClock());
        Assert.Equal(email, (await reader.GetProfile.HandleAsync(userId, Ct)).Value!.Email);
    }

    [Fact]
    public async Task Ac5_language_is_saved_a_repeat_succeeds_and_an_unsupported_value_is_rejected()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        var first = await rig.UpdateLanguage.HandleAsync(new UpdateMyLanguageCommand(userId, "en"), Ct);
        var again = await rig.UpdateLanguage.HandleAsync(new UpdateMyLanguageCommand(userId, "en"), Ct);
        var bad = await rig.UpdateLanguage.HandleAsync(new UpdateMyLanguageCommand(userId, "fr"), Ct);

        Assert.Equal("en", first.Value);
        Assert.Equal("en", again.Value);
        Assert.Equal("identity.language_unsupported", bad.Failure!.Violations!.Single().Code);
        await using var reader = new Rig(postgres, Rig.NewClock());
        Assert.Equal("en", (await reader.GetProfile.HandleAsync(userId, Ct)).Value!.Language);
    }

    [Fact]
    public async Task Ac8_a_name_change_writes_exactly_one_event_that_matches_the_schema_and_has_no_private_fields()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();

        await rig.UpdateProfile.HandleAsync(Edit(userId, "Lan Tran", "0912345678", "female", "1995-04-30"), Ct);

        var events = await EventsOfAsync(userId);
        var data = Assert.Single(events);
        Assert.Equal(["displayName"], data.GetProperty("changedFields").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal((email, "Lan Tran", "vi"), (data.GetProperty("email").GetString(), data.GetProperty("displayName").GetString(), data.GetProperty("language").GetString()));
        foreach (var forbidden in new[] { "phone", "gender", "dateOfBirth" })
        {
            Assert.False(data.TryGetProperty(forbidden, out _), $"{forbidden} must not leave Identity");
        }

        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct));
        Assert.True(schema.Evaluate(data).IsValid, "UserProfileChanged data does not match its schema");
    }

    [Fact]
    public async Task Ac8_a_language_change_writes_one_event_and_a_repeat_writes_none()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        await rig.UpdateLanguage.HandleAsync(new UpdateMyLanguageCommand(userId, "en"), Ct);
        await rig.UpdateLanguage.HandleAsync(new UpdateMyLanguageCommand(userId, "en"), Ct);

        var data = Assert.Single(await EventsOfAsync(userId));
        Assert.Equal(["language"], data.GetProperty("changedFields").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("en", data.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Ac8_a_phone_only_or_unchanged_edit_writes_no_event()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        await rig.UpdateProfile.HandleAsync(Edit(userId, "An", "0912345678", "male", "1990-01-01"), Ct);
        await rig.UpdateProfile.HandleAsync(Edit(userId, "An", "0912345678", "male", "1990-01-01"), Ct);

        Assert.Empty(await EventsOfAsync(userId));
    }

    [Theory]
    [InlineData(61, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public async Task Ac11_registration_accepts_a_display_name_of_up_to_80_characters_and_rejects_81(int length, bool accepted)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.Register.HandleAsync(new RegisterUserCommand(Rig.NewEmail(), new string('n', length), "hello1234", "vi"), Ct);

        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
        {
            Assert.Equal("identity.display_name_too_long", result.Failure!.Violations!.Single().Code);
        }
    }

    private async Task<List<System.Text.Json.JsonElement>> EventsOfAsync(Guid userId)
    {
        await using var db = postgres.CreateContext();
        var rows = await db.Set<OutboxMessage>().Where(m => m.EventType == Event).ToListAsync(Ct);
        return rows.Select(r => EventEnvelope.FromJson(r.Envelope).Data).Where(d => d.GetProperty("userId").GetGuid() == userId).ToList();
    }

    private static string SchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "UserProfileChanged", "v1.schema.json");
    }
}
