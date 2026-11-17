using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.IntegrationTests;

public sealed class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_creates_tables_and_seeds_the_skeleton_category_with_diacritics()
    {
        await using var db = postgres.CreateContext();

        var category = await db.Categories.SingleAsync(c => c.Slug == "thoi-trang", TestContext.Current.CancellationToken);

        // NFR-L10N-003: Vietnamese text survives the round trip.
        Assert.Equal("Thời trang", category.NameVi);
        Assert.Equal("Fashion", category.NameEn);
    }

    [Fact]
    public async Task Outbox_row_is_stored_with_its_envelope()
    {
        var envelope = EventEnvelope.Create("Ping", 1, "catalog", "test-correlation", new { message = "hi" });
        await using (var db = postgres.CreateContext())
        {
            db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = postgres.CreateContext();
        var stored = await read.Set<OutboxMessage>().SingleAsync(m => m.Id == envelope.EventId, TestContext.Current.CancellationToken);
        Assert.Null(stored.SentAt);
        Assert.Equal("Ping", EventEnvelope.FromJson(stored.Envelope).EventType);
    }
}
