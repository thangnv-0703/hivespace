using HiveSpace.BuildingBlocks.Messaging;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>Identity's database (<c>identitydb</c>): users, credentials, roles, verification tokens, outbox/inbox and the OpenIddict stores.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint("ck_users_language", "language in ('vi', 'en')"));
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).ValueGeneratedNever();
            e.Property(u => u.EmailAddress).HasColumnName("email").HasMaxLength(254);
            e.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254);
            e.HasIndex(u => u.NormalizedEmail).IsUnique();
            e.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(60);
            e.Property(u => u.Language).HasColumnName("language").HasMaxLength(2).HasDefaultValue("vi");
            e.Property(u => u.EmailVerifiedAt).HasColumnName("email_verified_at");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            e.Ignore(u => u.IsEmailVerified);
            e.HasMany(u => u.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(u => u.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RoleGrant>(e =>
        {
            e.ToTable("role_grants", t => t.HasCheckConstraint("ck_role_grants_role", "role in ('Buyer', 'Seller')"));
            e.HasKey(r => new { r.UserId, r.Role });
            e.Property(r => r.UserId).HasColumnName("user_id");
            e.Property(r => r.Role).HasColumnName("role").HasMaxLength(16).HasConversion<string>();
            e.Property(r => r.GrantedAt).HasColumnName("granted_at");
        });

        modelBuilder.Entity<Credential>(e =>
        {
            e.ToTable("credentials");
            e.HasKey(c => c.UserId);
            e.Property(c => c.UserId).HasColumnName("user_id").ValueGeneratedNever();
            e.Property(c => c.PasswordHash).HasColumnName("password_hash");
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<User>().WithOne().HasForeignKey<Credential>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VerificationToken>(e =>
        {
            e.ToTable("verification_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.UserId).HasColumnName("user_id");
            e.HasIndex(t => t.UserId);
            e.Property(t => t.TokenHash).HasColumnName("token_hash");
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            e.Property(t => t.UsedAt).HasColumnName("used_at");
            e.Property(t => t.InvalidatedAt).HasColumnName("invalidated_at");
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.AddOutboxAndInbox();
        modelBuilder.UseOpenIddict();
    }
}
