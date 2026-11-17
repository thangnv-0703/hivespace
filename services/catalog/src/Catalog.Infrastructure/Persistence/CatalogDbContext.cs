using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>Seed row for the walking skeleton. FEAT-014 replaces it with the seeded category tree.</summary>
    public static readonly Category SkeletonCategory = new(
        Guid.Parse("01930000-0000-7000-8000-000000000001"),
        "thoi-trang",
        "Thời trang",
        "Fashion");

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Slug).HasMaxLength(120);
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.NameVi).HasMaxLength(200);
            e.Property(c => c.NameEn).HasMaxLength(200);
            e.HasData(SkeletonCategory);
        });

        modelBuilder.AddOutboxAndInbox();
    }
}
