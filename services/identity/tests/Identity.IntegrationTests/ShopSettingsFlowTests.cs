using System.Text;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.Shops;
using Identity.Domain;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Shop settings handlers against a real Postgres: AC-1..AC-7, AC-9, AC-10, AC-11 (handler level, FEAT-010 T5).</summary>
public sealed class ShopSettingsFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Digits = "0071001234567";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] None = [];

    private async Task<(Guid UserId, Guid ShopId, Rig Rig)> SellerAsync(string? name = null)
    {
        var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, token) = await rig.RegisterAsync();
        Assert.True((await rig.Verify.HandleAsync(token, Ct)).IsSuccess);
        var shop = await rig.RegisterShop.HandleAsync(new RegisterShopCommand(userId, name ?? $"Tiệm Bánh {Guid.NewGuid():N}", "individual", None), Ct);
        Assert.True(shop.IsSuccess, shop.Failure?.Code);
        return (userId, shop.Value!.ShopId, rig);
    }

    private static UpdateShopProfileCommand Profile(Guid userId, string name, string? description = null, params string[] invalid) => new(userId, name, description, invalid);

    private static SetPickupAddressCommand Address(Guid userId, string phone = "0912 345 678") =>
        new(userId, "An Nguyen", phone, "Hà Nội", "Ba Đình", "Phúc Xá", "12 Lê Lợi", None);

    private static SetPayoutCommand Payout(Guid userId, string number = "0071 0012 34567") => new(userId, "Vietcombank", number, "NGUYEN VAN AN", None);

    private async Task<List<(string Type, EventEnvelope Envelope)>> OutboxAsync(Guid shopId, string type = "ShopChanged")
    {
        await using var read = postgres.CreateContext();
        return (await read.Set<OutboxMessage>().Where(m => m.EventType == type).OrderBy(m => m.OccurredAt).ToListAsync(Ct))
            .Select(r => (r.EventType, Envelope: EventEnvelope.FromJson(r.Envelope)))
            .Where(r => r.Envelope.CorrelationId == shopId.ToString())
            .ToList();
    }

    // ---- AC-1, AC-2 ----

    [Fact]
    public async Task Ac1_the_owner_updates_name_and_description_and_null_clears_the_description()
    {
        var (userId, _, rig) = await SellerAsync();
        await using var _ = rig;
        var name = $"Tiệm Bánh An {Guid.NewGuid():N}";

        var saved = await rig.UpdateShopProfile.HandleAsync(Profile(userId, name, "Bánh mì nóng\r\nmỗi sáng"), Ct);

        Assert.True(saved.IsSuccess, saved.Failure?.Code);
        Assert.Equal((name, "Bánh mì nóng\nmỗi sáng"), (saved.Value!.Name, saved.Value.Description));
        await using var next = new Rig(postgres, Rig.NewClock());
        Assert.Equal("Bánh mì nóng\nmỗi sáng", (await next.GetMyShop.HandleAsync(userId, Ct)).Value!.Description);

        var cleared = await next.UpdateShopProfile.HandleAsync(Profile(userId, name, null), Ct);
        Assert.Null(cleared.Value!.Description);
    }

    [Fact]
    public async Task Ac2_a_name_taken_by_another_shop_in_another_case_is_409_and_the_own_name_is_accepted()
    {
        var other = $"Cửa Hàng {Guid.NewGuid():N}";
        var (_, _, otherRig) = await SellerAsync(other);
        await using var __ = otherRig;
        var (userId, shopId, rig) = await SellerAsync();
        await using var _ = rig;

        var taken = await rig.UpdateShopProfile.HandleAsync(Profile(userId, "  " + other.ToUpperInvariant().Replace(" ", "   ", StringComparison.Ordinal)), Ct);

        Assert.Equal("identity.shop_name_taken", taken.Failure?.Code);
        Assert.Empty(await OutboxAsync(shopId));

        await using var next = new Rig(postgres, Rig.NewClock());
        var current = (await next.GetMyShop.HandleAsync(userId, Ct)).Value!;
        var sameNameOtherCase = await next.UpdateShopProfile.HandleAsync(Profile(userId, current.Name.ToUpperInvariant()), Ct);
        Assert.True(sameNameOtherCase.IsSuccess, sameNameOtherCase.Failure?.Code);
        Assert.Equal(current.Name.ToUpperInvariant(), sameNameOtherCase.Value!.Name);
    }

    [Fact]
    public async Task A_name_conflict_found_only_at_commit_is_also_409()
    {
        var other = $"Cửa Hàng {Guid.NewGuid():N}";
        var (_, _, otherRig) = await SellerAsync(other);
        await using var __ = otherRig;
        var (userId, _, rig) = await SellerAsync();
        await using var _ = rig;

        // Blind to the name check, as when a parallel rename commits just after it: the unique index decides.
        var result = await rig.UpdateShopProfileBlindToNames().HandleAsync(Profile(userId, other), Ct);

        Assert.Equal("identity.shop_name_taken", result.Failure?.Code);
    }

    // ---- AC-3, AC-4, AC-5 ----

    [Fact]
    public async Task Ac3_the_pickup_address_is_saved_trimmed_with_an_E164_phone_and_flips_the_checklist()
    {
        var (userId, _, rig) = await SellerAsync();
        await using var _ = rig;

        var saved = await rig.SetPickupAddress.HandleAsync(Address(userId) with { Street = "  12   Lê Lợi " }, Ct);

        Assert.True(saved.IsSuccess, saved.Failure?.Code);
        Assert.Equal(new PickupAddressView("An Nguyen", "+84912345678", "Hà Nội", "Ba Đình", "Phúc Xá", "12 Lê Lợi"), saved.Value!.PickupAddress);
        Assert.Equal(new ShopSetupView(true, false), saved.Value.Setup);
    }

    [Fact]
    public async Task Ac4_ac5_the_payout_returns_the_last_four_only_and_the_database_holds_ciphertext()
    {
        var (userId, shopId, rig) = await SellerAsync();
        await using var _ = rig;

        var saved = await rig.SetPayout.HandleAsync(Payout(userId), Ct);

        Assert.True(saved.IsSuccess, saved.Failure?.Code);
        Assert.Equal(new PayoutView("Vietcombank", "4567", "NGUYEN VAN AN"), saved.Value!.Payout);
        Assert.Equal(new ShopSetupView(false, true), saved.Value.Setup);
        Assert.DoesNotContain(Digits, System.Text.Json.JsonSerializer.Serialize(saved.Value));

        await using var read = postgres.CreateContext();
        var stored = (await read.Shops.SingleAsync(s => s.Id == shopId, Ct)).Payout!;
        Assert.DoesNotContain(Digits, Encoding.UTF8.GetString(stored.AccountNumber.Bytes));
        Assert.DoesNotContain(Digits, Encoding.Latin1.GetString(stored.AccountNumber.Bytes));
        Assert.Equal(Digits, Rig.Protector.Unprotect(stored.AccountNumber, shopId).Digits);
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => Rig.Protector.Unprotect(stored.AccountNumber, Guid.NewGuid()));
    }

    // ---- AC-6 ----

    [Fact]
    public async Task Ac6_every_invalid_field_is_reported_once_and_nothing_is_saved_or_queued()
    {
        var (userId, shopId, rig) = await SellerAsync();
        await using var _ = rig;

        var profile = await rig.UpdateShopProfile.HandleAsync(new UpdateShopProfileCommand(userId, "ab", new string('d', 1001), ["shopId", "name"]), Ct);
        var address = await rig.SetPickupAddress.HandleAsync(new SetPickupAddressCommand(userId, "", "12345", new string('p', 61), "d\u0007", " ", new string('s', 201), ["extra"]), Ct);
        var payout = await rig.SetPayout.HandleAsync(new SetPayoutCommand(userId, new string('b', 81), "12ab", "", ["extra"]), Ct);

        Assert.Equal(
            [("shopId", "common.validation_failed"), ("name", "common.validation_failed"), ("description", "identity.shop_description_too_long")],
            profile.Failure!.Violations!.Select(v => (v.Field, v.Code)).ToArray());
        Assert.Equal(
            [
                ("extra", "common.validation_failed"), ("contactName", "identity.address_field_required"), ("phone", "identity.phone_invalid"),
                ("province", "identity.address_field_too_long"), ("district", "identity.address_field_invalid"),
                ("ward", "identity.address_field_required"), ("street", "identity.address_field_too_long"),
            ],
            address.Failure!.Violations!.Select(v => (v.Field, v.Code)).ToArray());
        Assert.Equal(
            [("extra", "common.validation_failed"), ("bankName", "identity.bank_name_too_long"), ("accountNumber", "identity.bank_account_number_invalid"),
                ("accountHolder", "identity.bank_account_holder_required")],
            payout.Failure!.Violations!.Select(v => (v.Field, v.Code)).ToArray());

        await using var read = postgres.CreateContext();
        var shop = await read.Shops.SingleAsync(s => s.Id == shopId, Ct);
        Assert.Equal((null, null, null, 1), (shop.Description, shop.PickupAddress, shop.Payout, shop.ProfileVersion));
        Assert.Empty(await OutboxAsync(shopId));
    }

    // ---- AC-7 ----

    [Fact]
    public async Task Ac7_a_new_shop_reads_null_sections_and_after_three_saves_reads_all_of_them()
    {
        var (userId, _, rig) = await SellerAsync();
        await using var _ = rig;
        var before = (await rig.GetMyShop.HandleAsync(userId, Ct)).Value!;
        Assert.Equal((null, null, null, new ShopSetupView(false, false)), (before.Description, before.PickupAddress, before.Payout, before.Setup));

        await rig.UpdateShopProfile.HandleAsync(Profile(userId, before.Name, "Mô tả"), Ct);
        await rig.SetPickupAddress.HandleAsync(Address(userId), Ct);
        await rig.SetPayout.HandleAsync(Payout(userId), Ct);

        await using var next = new Rig(postgres, Rig.NewClock());
        var after = (await next.GetMyShop.HandleAsync(userId, Ct)).Value!;
        Assert.Equal(("Mô tả", "+84912345678", "4567", new ShopSetupView(true, true)), (after.Description, after.PickupAddress!.Phone, after.Payout!.AccountNumberLast4, after.Setup));
    }

    [Fact]
    public async Task A_user_without_a_shop_gets_not_found_on_every_save()
    {
        var rig = new Rig(postgres, Rig.NewClock());
        await using var _ = rig;
        var (userId, _, _) = await rig.RegisterAsync();

        Assert.Equal("common.not_found", (await rig.UpdateShopProfile.HandleAsync(Profile(userId, "Tiệm Bánh An"), Ct)).Failure?.Code);
        Assert.Equal("common.not_found", (await rig.SetPickupAddress.HandleAsync(Address(userId), Ct)).Failure?.Code);
        Assert.Equal("common.not_found", (await rig.SetPayout.HandleAsync(Payout(userId), Ct)).Failure?.Code);
    }

    // ---- AC-9 ----

    [Fact]
    public async Task Ac9_events_follow_profile_changes_and_the_readiness_flip_and_match_the_schema()
    {
        var (userId, shopId, rig) = await SellerAsync();
        await using var _ = rig;
        var name = $"Tiệm Mới {Guid.NewGuid():N}";

        await rig.UpdateShopProfile.HandleAsync(Profile(userId, name, "Mô tả"), Ct);
        await rig.UpdateShopProfile.HandleAsync(Profile(userId, name, "Mô tả"), Ct);
        Assert.Single(await OutboxAsync(shopId));

        await rig.SetPickupAddress.HandleAsync(Address(userId), Ct);
        Assert.Single(await OutboxAsync(shopId));
        await rig.SetPayout.HandleAsync(Payout(userId), Ct);
        await rig.SetPayout.HandleAsync(Payout(userId, "111222333444"), Ct);
        await rig.SetPickupAddress.HandleAsync(Address(userId, "0987654321"), Ct);

        var rows = await OutboxAsync(shopId);
        Assert.Equal(2, rows.Count);
        var versions = rows.Select(r => r.Envelope.Data.GetProperty("version").GetInt32()).ToArray();
        Assert.Equal([2, 3], versions);
        Assert.Equal([false, true], rows.Select(r => r.Envelope.Data.GetProperty("setupComplete").GetBoolean()).ToArray());

        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct));
        foreach (var row in rows)
        {
            Assert.True(schema.Evaluate(row.Envelope.Data).IsValid, "ShopChanged data does not match its schema");
            var raw = row.Envelope.Data.GetRawText();
            Assert.DoesNotContain("pickup", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("phone", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bank", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("account", raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- AC-10 ----

    [Fact]
    public async Task Ac10_two_concurrent_profile_updates_both_succeed_and_raise_the_version_by_two()
    {
        var (userId, shopId, firstRig) = await SellerAsync();
        await using var _ = firstRig;
        await using var secondRig = new Rig(postgres, Rig.NewClock());
        var suffix = Guid.NewGuid().ToString("N");

        var results = await Task.WhenAll(
            firstRig.UpdateShopProfile.HandleAsync(Profile(userId, $"Một {suffix}", "a"), Ct),
            secondRig.UpdateShopProfile.HandleAsync(Profile(userId, $"Hai {suffix}", "b"), Ct));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Failure?.Code));
        await using var read = postgres.CreateContext();
        Assert.Equal(3, (await read.Shops.SingleAsync(s => s.Id == shopId, Ct)).ProfileVersion);
        Assert.Equal(2, (await OutboxAsync(shopId)).Count);
    }

    // ---- AC-11 ----

    [Fact]
    public async Task Ac11_no_log_line_while_saving_a_payout_contains_the_account_digits()
    {
        var (userId, _, _) = await SellerAsync();
        var logs = new CapturedLogs();
        await using var db = postgres.CreateContext(logs);
        var users = new Identity.Infrastructure.Persistence.EfUserRepository(db);
        var outbox = new EfEventOutbox<Identity.Infrastructure.Persistence.IdentityDbContext>(db, new MessagingOptions("hivespace-events", "identity"));
        var handler = new SetPayoutHandler(users, users, Rig.Protector, outbox, TimeProvider.System);
        var command = Payout(userId);

        var saved = await handler.HandleAsync(command, Ct);

        Assert.True(saved.IsSuccess, saved.Failure?.Code);
        Assert.NotEmpty(logs.Text);
        Assert.DoesNotContain(Digits, logs.Text);
        Assert.DoesNotContain(Digits, command.ToString());
        Assert.DoesNotContain("0071", $"{BankAccountNumber.Create(command.AccountNumber).Number}");
    }

    private static string SchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "contracts", "events", "ShopChanged", "v1.schema.json");
    }
}
