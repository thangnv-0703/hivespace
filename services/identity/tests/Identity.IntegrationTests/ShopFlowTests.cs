using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.Shops;
using Identity.Domain;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Shop registration handlers against a real Postgres: AC-1..AC-9 (handler level, FEAT-009 T4).</summary>
public sealed class ShopFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The database is shared by the class, so a default name is unique per call.
    private static RegisterShopCommand Form(Guid userId, string? name = null, string? type = "individual", params string[] unknown) =>
        new(userId, name ?? $"Tiệm Bánh {Guid.NewGuid():N}", type, unknown);

    private async Task<(Guid UserId, Rig Rig)> VerifiedBuyerAsync()
    {
        var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, token) = await rig.RegisterAsync();
        Assert.True((await rig.Verify.HandleAsync(token, Ct)).IsSuccess);
        return (userId, rig);
    }

    [Fact]
    public async Task Ac1_ac2_ac9_a_verified_buyer_gets_a_shop_both_roles_and_one_audit_row()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        var unique = Guid.NewGuid().ToString("N");
        var result = await rig.RegisterShop.HandleAsync(Form(userId, $"Tiệm  Bánh {unique} ", "business"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var shop = result.Value!;
        Assert.Equal(($"Tiệm Bánh {unique}", "business", false, false), (shop.Name, shop.SellerType, shop.Setup.PickupAddress, shop.Setup.Payout));

        await using var read = postgres.CreateContext();
        var stored = await read.Shops.SingleAsync(s => s.OwnerUserId == userId, Ct);
        Assert.Equal(shop.ShopId, stored.Id);
        Assert.Equal($"tiệm bánh {unique}", stored.NormalizedName);
        var roles = await read.Set<RoleGrant>().Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync(Ct);
        Assert.Equivalent(new[] { Role.Buyer, Role.Seller }, roles);

        var audit = await read.AuditEntries.SingleAsync(a => a.UserId == userId && a.Type == "seller_role_granted", Ct);
        Assert.Null(audit.Method);
        Assert.Null(audit.Reason);
    }

    [Fact]
    public async Task Ac8_each_registration_writes_one_ShopRegistered_row_that_matches_its_schema()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        var shop = (await rig.RegisterShop.HandleAsync(Form(userId), Ct)).Value!;

        await using var read = postgres.CreateContext();
        var rows = (await read.Set<OutboxMessage>().Where(m => m.EventType == "ShopRegistered").ToListAsync(Ct))
            .Select(r => (Row: r, Envelope: EventEnvelope.FromJson(r.Envelope)))
            .Where(r => r.Envelope.Data.GetProperty("ownerUserId").GetGuid() == userId)
            .ToList();
        var row = Assert.Single(rows);
        Assert.Equal(shop.ShopId.ToString(), row.Envelope.CorrelationId);
        var data = row.Envelope.Data;
        Assert.Equal((shop.ShopId, shop.Name, "individual"), (data.GetProperty("shopId").GetGuid(), data.GetProperty("name").GetString(), data.GetProperty("sellerType").GetString()));

        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct));
        Assert.True(schema.Evaluate(data).IsValid, "ShopRegistered data does not match its schema");
    }

    [Fact]
    public async Task Ac3_every_invalid_field_is_reported_together_and_nothing_is_saved()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        var result = await rig.RegisterShop.HandleAsync(Form(userId, "ab", "x", "ownerId"), Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        Assert.Equivalent(
            new[] { ("ownerId", "common.validation_failed"), ("name", "identity.shop_name_too_short"), ("sellerType", "identity.seller_type_invalid") },
            result.Failure!.Violations!.Select(v => (v.Field, v.Code)).ToArray());
        await AssertNothingSavedAsync(userId);
    }

    [Theory]
    [InlineData(null, "identity.shop_name_required")]
    [InlineData("  ", "identity.shop_name_required")]
    [InlineData("a", "identity.shop_name_too_short")]
    [InlineData("Shop\u0007Name", "identity.shop_name_invalid")]
    public async Task Ac3_each_name_rule_has_its_own_code_and_saves_nothing(string? name, string code)
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        var result = await rig.RegisterShop.HandleAsync(new RegisterShopCommand(userId, name, "individual", []), Ct);

        Assert.Equal(code, Assert.Single(result.Failure!.Violations!).Code);
        await AssertNothingSavedAsync(userId);
    }

    [Fact]
    public async Task Ac3_a_61_character_name_is_too_long()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        var result = await rig.RegisterShop.HandleAsync(Form(userId, new string('a', 61)), Ct);

        Assert.Equal("identity.shop_name_too_long", Assert.Single(result.Failure!.Violations!).Code);
    }

    [Fact]
    public async Task Ac4_an_unverified_email_gets_email_not_verified_and_no_shop()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        var result = await rig.RegisterShop.HandleAsync(Form(userId), Ct);

        Assert.Equal("identity.email_not_verified", result.Failure?.Code);
        await AssertNothingSavedAsync(userId);
    }

    [Fact]
    public async Task Ac4_a_user_who_verified_after_the_token_was_issued_can_register_because_the_database_is_read()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, token) = await rig.RegisterAsync();
        Assert.Equal("identity.email_not_verified", (await rig.RegisterShop.HandleAsync(Form(userId), Ct)).Failure?.Code);

        await rig.Verify.HandleAsync(token, Ct);
        await using var next = new Rig(postgres, Rig.NewClock());

        Assert.True((await next.RegisterShop.HandleAsync(Form(userId), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Ac5_a_second_shop_for_the_same_owner_is_shop_already_owned()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;
        await rig.RegisterShop.HandleAsync(Form(userId), Ct);
        await using var next = new Rig(postgres, Rig.NewClock());

        var result = await next.RegisterShop.HandleAsync(Form(userId, "Another Shop", "business"), Ct);

        Assert.Equal("identity.shop_already_owned", result.Failure?.Code);
        await using var read = postgres.CreateContext();
        Assert.Equal(1, await read.Shops.CountAsync(s => s.OwnerUserId == userId, Ct));
    }

    [Fact]
    public async Task Ac6_a_name_taken_in_another_case_and_spacing_is_shop_name_taken()
    {
        var (first, firstRig) = await VerifiedBuyerAsync();
        await using var _ = firstRig;
        var (second, secondRig) = await VerifiedBuyerAsync();
        await using var __ = secondRig;
        var name = $"Tiệm Bánh {Guid.NewGuid():N}";
        await firstRig.RegisterShop.HandleAsync(Form(first, name), Ct);

        var result = await secondRig.RegisterShop.HandleAsync(Form(second, name.ToUpperInvariant().Replace(" ", "   ") + " "), Ct);

        Assert.Equal("identity.shop_name_taken", result.Failure?.Code);
        await AssertNothingSavedAsync(second);
    }

    [Fact]
    public async Task Ac5_two_parallel_registrations_by_one_user_create_one_shop_and_one_409()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;
        await using var a = new Rig(postgres, Rig.NewClock());
        await using var b = new Rig(postgres, Rig.NewClock());

        var results = await Task.WhenAll(
            a.RegisterShop.HandleAsync(Form(userId, "Parallel Own One"), Ct),
            b.RegisterShop.HandleAsync(Form(userId, "Parallel Own Two"), Ct));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal("identity.shop_already_owned", Assert.Single(results, r => !r.IsSuccess).Failure?.Code);
        await using var read = postgres.CreateContext();
        Assert.Equal(1, await read.Shops.CountAsync(s => s.OwnerUserId == userId, Ct));
    }

    [Fact]
    public async Task Ac6_two_parallel_registrations_of_one_name_by_two_users_create_one_shop()
    {
        var (first, firstRig) = await VerifiedBuyerAsync();
        await using var _ = firstRig;
        var (second, secondRig) = await VerifiedBuyerAsync();
        await using var __ = secondRig;
        var name = $"Parallel Name {Guid.NewGuid():N}";

        var results = await Task.WhenAll(
            firstRig.RegisterShop.HandleAsync(Form(first, name), Ct),
            secondRig.RegisterShop.HandleAsync(Form(second, name.ToUpperInvariant()), Ct));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal("identity.shop_name_taken", Assert.Single(results, r => !r.IsSuccess).Failure?.Code);
        await using var read = postgres.CreateContext();
        Assert.Equal(1, await read.Shops.CountAsync(s => s.NormalizedName == name.ToLowerInvariant(), Ct));
    }

    [Fact]
    public async Task Ac7_a_name_conflict_at_commit_leaves_no_grant_audit_or_outbox_row()
    {
        var (first, firstRig) = await VerifiedBuyerAsync();
        await using var _ = firstRig;
        var (second, secondRig) = await VerifiedBuyerAsync();
        await using var __ = secondRig;
        var name = $"Conflict {Guid.NewGuid():N}";
        await firstRig.RegisterShop.HandleAsync(Form(first, name), Ct);

        // The loser passes the name check (it ran before the winner committed) but fails at the unique index.
        var result = await secondRig.RegisterShopBlindToNames().HandleAsync(Form(second, name), Ct);

        Assert.Equal("identity.shop_name_taken", result.Failure?.Code);
        await AssertNothingSavedAsync(second);
    }

    [Fact]
    public async Task Get_returns_the_owners_shop_and_not_found_for_a_user_without_one()
    {
        var (userId, rig) = await VerifiedBuyerAsync();
        await using var _ = rig;

        Assert.Equal("common.not_found", (await rig.GetMyShop.HandleAsync(userId, Ct)).Failure?.Code);
        var created = (await rig.RegisterShop.HandleAsync(Form(userId), Ct)).Value!;
        await using var next = new Rig(postgres, Rig.NewClock());

        var read = (await next.GetMyShop.HandleAsync(userId, Ct)).Value!;
        Assert.Equal((created.ShopId, created.Name, created.SellerType, created.Setup), (read.ShopId, read.Name, read.SellerType, read.Setup));
    }

    private async Task AssertNothingSavedAsync(Guid userId)
    {
        await using var read = postgres.CreateContext();
        Assert.False(await read.Shops.AnyAsync(s => s.OwnerUserId == userId, Ct));
        Assert.Equal([Role.Buyer], await read.Set<RoleGrant>().Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync(Ct));
        Assert.False(await read.AuditEntries.AnyAsync(a => a.UserId == userId && a.Type == "seller_role_granted", Ct));
        var outbox = (await read.Set<OutboxMessage>().Where(m => m.EventType == "ShopRegistered").ToListAsync(Ct))
            .Select(r => EventEnvelope.FromJson(r.Envelope).Data.GetProperty("ownerUserId").GetGuid());
        Assert.DoesNotContain(userId, outbox);
    }

    private static string SchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "ShopRegistered", "v1.schema.json");
    }
}
