using Identity.Application.Abstractions;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-009 T3: each unique index of <c>shops</c> surfaces as its own typed exception from <c>SaveChangesAsync</c>.</summary>
public sealed class ShopPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_second_shop_for_one_owner_raises_ShopAlreadyOwnedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AddUserAsync(ct);
        await AddShopAsync(owner, "Tiệm Bánh An", ct);

        await using var db = postgres.CreateContext();
        var repo = new EfUserRepository(db);
        repo.Add(NewShop(owner, "Another Name"));

        await Assert.ThrowsAsync<ShopAlreadyOwnedException>(() => repo.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task A_second_shop_with_the_same_normalized_name_raises_ShopNameTakenException()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await AddUserAsync(ct);
        var second = await AddUserAsync(ct);
        await AddShopAsync(first, "Cửa Hàng Lan", ct);

        await using var db = postgres.CreateContext();
        var repo = new EfUserRepository(db);
        repo.Add(NewShop(second, "cửa  hàng lan "));

        await Assert.ThrowsAsync<ShopNameTakenException>(() => repo.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task The_store_finds_a_shop_by_owner_and_a_name_by_its_normalized_form()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AddUserAsync(ct);
        await AddShopAsync(owner, "Quán Trà Mơ", ct);

        await using var db = postgres.CreateContext();
        var repo = new EfUserRepository(db);

        Assert.Equal("Quán Trà Mơ", (await repo.FindByOwnerAsync(owner, ct))!.Name);
        Assert.Null(await repo.FindByOwnerAsync(Guid.NewGuid(), ct));
        Assert.True(await repo.NameExistsAsync("quán trà mơ", ct));
        Assert.False(await repo.NameExistsAsync("quan tra mo", ct));
    }

    [Fact]
    public async Task A_role_grant_audit_row_is_stored_without_a_method_and_read_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AddUserAsync(ct);

        await using (var db = postgres.CreateContext())
        {
            var repo = new EfUserRepository(db);
            repo.Add(AuditEntry.SellerRoleGranted(owner, Now));
            await repo.SaveChangesAsync(ct);
        }

        await using var read = postgres.CreateContext();
        var entry = read.AuditEntries.Single(a => a.UserId == owner);
        Assert.Equal("seller_role_granted", entry.Type);
        Assert.Null(entry.Method);
    }

    private async Task<Guid> AddUserAsync(CancellationToken ct)
    {
        await using var db = postgres.CreateContext();
        var id = Guid.CreateVersion7();
        var (email, _) = Email.Create($"{id:N}@example.com");
        var (name, _) = DisplayName.Create("Owner");
        var user = User.Register(email!, name!, "vi", Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    private async Task AddShopAsync(Guid owner, string name, CancellationToken ct)
    {
        await using var db = postgres.CreateContext();
        var repo = new EfUserRepository(db);
        repo.Add(NewShop(owner, name));
        await repo.SaveChangesAsync(ct);
    }

    private static Shop NewShop(Guid owner, string name) =>
        Shop.Register(owner, ShopName.Create(name).Name!, SellerType.Create("individual").Type!, Now);
}
