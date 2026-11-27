using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<AttributeDefinition> Attributes => Set<AttributeDefinition>();

    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();

    public DbSet<CategoryAttribute> CategoryAttributes => Set<CategoryAttribute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ids are the source (Tiki) keys from the load file, never generated (FEAT-014 D-9).
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories", t => t.HasCheckConstraint("CK_categories_ImageUrl_TopLevel", "\"ImageUrl\" IS NULL OR \"ParentId\" IS NULL"));
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.ComplexProperty(c => c.Name, n =>
            {
                n.Property(x => x.Vi).HasColumnName("NameVi").HasMaxLength(200);
                n.Property(x => x.En).HasColumnName("NameEn").HasMaxLength(200);
            });
            e.Property(c => c.ImageUrl).HasMaxLength(500);
            e.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.ParentId, c.Position });
        });

        // List settings live on the attribute, shared by every category that links it (D-11).
        modelBuilder.Entity<AttributeDefinition>(e =>
        {
            e.ToTable("attributes", t =>
            {
                t.HasCheckConstraint("CK_attributes_Kind", "\"Kind\" IN ('list', 'text')");
                t.HasCheckConstraint(
                    "CK_attributes_MaxValues",
                    "(\"Kind\" = 'text' AND \"MaxValues\" = 1 AND NOT \"AllowsMultiple\") OR " +
                    "(\"Kind\" = 'list' AND ((\"AllowsMultiple\" AND \"MaxValues\" >= 2) OR (NOT \"AllowsMultiple\" AND \"MaxValues\" = 1)))");
                t.HasCheckConstraint("CK_attributes_AllowsCustomValue", "NOT \"AllowsCustomValue\" OR \"Kind\" = 'list'");
            });
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedNever();
            e.ComplexProperty(a => a.Name, n =>
            {
                n.Property(x => x.Vi).HasColumnName("NameVi").HasMaxLength(200);
                n.Property(x => x.En).HasColumnName("NameEn").HasMaxLength(200);
            });
            e.Property(a => a.Kind).HasMaxLength(8).HasConversion(new ValueConverter<AttributeKind, string>(
                k => k == AttributeKind.List ? "list" : "text",
                s => s == "list" ? AttributeKind.List : AttributeKind.Text));
        });

        modelBuilder.Entity<AttributeValue>(e =>
        {
            e.ToTable("attribute_values");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).ValueGeneratedNever();
            e.ComplexProperty(v => v.Name, n =>
            {
                n.Property(x => x.Vi).HasColumnName("NameVi").HasMaxLength(300);
                n.Property(x => x.En).HasColumnName("NameEn").HasMaxLength(300);
            });
            e.HasOne<AttributeDefinition>().WithMany().HasForeignKey(v => v.AttributeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(v => new { v.AttributeId, v.Position }).IsUnique();
        });

        modelBuilder.Entity<CategoryAttribute>(e =>
        {
            e.ToTable("category_attributes");
            e.HasKey(l => new { l.CategoryId, l.AttributeId });
            e.HasOne<Category>().WithMany().HasForeignKey(l => l.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AttributeDefinition>().WithMany().HasForeignKey(l => l.AttributeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.AttributeId);
        });

        modelBuilder.AddOutboxAndInbox();
    }
}
