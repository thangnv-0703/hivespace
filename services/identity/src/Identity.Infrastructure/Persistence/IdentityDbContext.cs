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

    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    public DbSet<SignInLockout> SignInLockouts => Set<SignInLockout>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<SignInCodeChallenge> SignInCodeChallenges => Set<SignInCodeChallenge>();

    public DbSet<Shop> Shops => Set<Shop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_language", "language in ('vi', 'en')");
                t.HasCheckConstraint("ck_users_gender", "gender in ('male', 'female', 'other', 'prefer_not_to_say')");
            });
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(u => u.EmailAddress).HasColumnName("email").HasMaxLength(254);
            e.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254);
            e.HasIndex(u => u.NormalizedEmail).IsUnique();
            e.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(80);
            e.Property(u => u.Language).HasColumnName("language").HasMaxLength(2).HasDefaultValue("vi");
            e.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(16);
            e.Property(u => u.Gender).HasColumnName("gender").HasMaxLength(20);
            e.Property(u => u.DateOfBirth).HasColumnName("date_of_birth");
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
            e.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
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

        modelBuilder.Entity<ExternalLogin>(e =>
        {
            e.ToTable("external_logins");
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(l => l.UserId).HasColumnName("user_id");
            e.Property(l => l.Provider).HasColumnName("provider").HasMaxLength(ExternalLogin.ProviderMaxLength);
            e.Property(l => l.Subject).HasColumnName("subject").HasMaxLength(ExternalLogin.SubjectMaxLength);
            e.Property(l => l.LinkedAt).HasColumnName("linked_at");
            // One account per Google identity, and one Google identity per account (AC-11).
            e.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();
            e.HasIndex(l => new { l.UserId, l.Provider }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SignInLockout>(e =>
        {
            e.ToTable("sign_in_lockouts", t => t.HasCheckConstraint("ck_sign_in_lockouts_failed_count", "failed_count between 1 and 5"));
            e.HasKey(l => l.UserId);
            e.Property(l => l.UserId).HasColumnName("user_id").ValueGeneratedNever();
            e.Property(l => l.FailedCount).HasColumnName("failed_count");
            e.Property(l => l.WindowStartedAt).HasColumnName("window_started_at");
            e.Property(l => l.LockedUntil).HasColumnName("locked_until");
            // Postgres's own row version: a concurrent failure makes the update fail, and the recorder reapplies (design D-3).
            e.Property<uint>("Version").IsRowVersion();
            e.HasOne<User>().WithOne().HasForeignKey<SignInLockout>(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_entries", t =>
            {
                t.HasCheckConstraint("ck_audit_entries_type", "type in ('sign_in_succeeded','sign_in_failed','account_locked','seller_role_granted')");
                t.HasCheckConstraint("ck_audit_entries_method", "method in ('password','google','password_change','code')");
            });
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(a => a.OccurredAt).HasColumnName("occurred_at");
            e.Property(a => a.Type).HasColumnName("type").HasMaxLength(32);
            // Null for a role grant: it has no sign-in method (FEAT-009 D-9).
            e.Property(a => a.Method).HasColumnName("method").HasMaxLength(16).IsRequired(false)
                .HasConversion(m => m.HasValue ? (m == AuditMethod.PasswordChange ? "password_change" : m.Value.ToString().ToLowerInvariant()) : null,
                    v => v == null ? null : v == "password_change" ? AuditMethod.PasswordChange : Enum.Parse<AuditMethod>(v, true));
            // No foreign key: an audit row outlives nothing and blocks nothing (design §4).
            e.Property(a => a.UserId).HasColumnName("user_id");
            e.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(64);
            e.Property(a => a.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            e.HasIndex(a => a.OccurredAt);
            e.HasIndex(a => new { a.UserId, a.OccurredAt });
        });

        modelBuilder.Entity<Shop>(e =>
        {
            e.ToTable("shops", t =>
            {
                t.HasCheckConstraint("ck_shops_seller_type", "seller_type in ('individual','business')");
                // A half-filled section would mean a save was cut in the middle (FEAT-010 §4).
                t.HasCheckConstraint(
                    "ck_shops_pickup_all_or_none",
                    "(pickup_contact_name is null) = (pickup_phone is null) and (pickup_contact_name is null) = (pickup_province is null) "
                    + "and (pickup_contact_name is null) = (pickup_district is null) and (pickup_contact_name is null) = (pickup_ward is null) "
                    + "and (pickup_contact_name is null) = (pickup_street is null)");
                t.HasCheckConstraint(
                    "ck_shops_payout_all_or_none",
                    "(payout_bank_name is null) = (payout_account_holder is null) and (payout_bank_name is null) = (payout_account_ciphertext is null) "
                    + "and (payout_bank_name is null) = (payout_account_last4 is null) and (payout_bank_name is null) = (payout_key_id is null)");
            });
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(s => s.OwnerUserId).HasColumnName("owner_user_id");
            e.Property(s => s.Name).HasColumnName("name").HasMaxLength(ShopName.MaxLength);
            e.Property(s => s.NormalizedName).HasColumnName("normalized_name").HasMaxLength(ShopName.MaxLength);
            e.Property(s => s.SellerType).HasColumnName("seller_type").HasMaxLength(16);
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            e.Property(s => s.Description).HasColumnName("description").HasMaxLength(ShopDescription.MaxLength);
            e.Property(s => s.ProfileVersion).HasColumnName("profile_version").HasDefaultValue(1);
            // Postgres's own row version keeps profile_version and the readiness flip exact under concurrent saves (FEAT-010 D-9).
            e.Property<uint>("Version").IsRowVersion();
            e.OwnsOne(s => s.PickupAddress, a =>
            {
                a.Property(x => x.ContactName).HasColumnName("pickup_contact_name").HasMaxLength(80).IsRequired();
                a.Property(x => x.Phone).HasColumnName("pickup_phone").HasMaxLength(16).IsRequired();
                a.Property(x => x.Province).HasColumnName("pickup_province").HasMaxLength(60).IsRequired();
                a.Property(x => x.District).HasColumnName("pickup_district").HasMaxLength(60).IsRequired();
                a.Property(x => x.Ward).HasColumnName("pickup_ward").HasMaxLength(60).IsRequired();
                a.Property(x => x.Street).HasColumnName("pickup_street").HasMaxLength(200).IsRequired();
            });
            e.OwnsOne(s => s.Payout, p =>
            {
                p.Property(x => x.BankName).HasColumnName("payout_bank_name").HasMaxLength(80).IsRequired()
                    .HasConversion(v => v.Value, v => BankName.FromStored(v));
                p.Property(x => x.AccountHolder).HasColumnName("payout_account_holder").HasMaxLength(80).IsRequired()
                    .HasConversion(v => v.Value, v => AccountHolder.FromStored(v));
                p.Property(x => x.Last4).HasColumnName("payout_account_last4").HasColumnType("char(4)").IsRequired();
                p.OwnsOne(x => x.AccountNumber, n =>
                {
                    n.Property(x => x.Bytes).HasColumnName("payout_account_ciphertext").IsRequired();
                    n.Property(x => x.KeyId).HasColumnName("payout_key_id").HasMaxLength(32).IsRequired();
                });
                p.Navigation(x => x.AccountNumber).IsRequired();
            });
            // One shop per owner (FR-SHOP-002) and one normalized name per shop (FR-SHOP-003).
            e.HasIndex(s => s.OwnerUserId).IsUnique().HasDatabaseName("ix_shops_owner_user_id");
            e.HasIndex(s => s.NormalizedName).IsUnique().HasDatabaseName("ix_shops_normalized_name");
            e.HasOne<User>().WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SignInCodeChallenge>(e =>
        {
            e.ToTable("sign_in_code_challenges", t =>
            {
                t.HasCheckConstraint("ck_sign_in_code_challenges_failed_count", "failed_count between 0 and 5");
                t.HasCheckConstraint("ck_sign_in_code_challenges_end_reason", "end_reason in ('used','superseded','attempts')");
                t.HasCheckConstraint("ck_sign_in_code_challenges_ended", "(ended_at is null) = (end_reason is null)");
            });
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(c => c.EmailHash).HasColumnName("email_hash");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.CodeHash).HasColumnName("code_hash");
            e.Property(c => c.FailedCount).HasColumnName("failed_count").HasDefaultValue(0);
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.ExpiresAt).HasColumnName("expires_at");
            e.Property(c => c.EndedAt).HasColumnName("ended_at");
            e.Property(c => c.EndReason).HasColumnName("end_reason").HasMaxLength(16)
                .HasConversion(r => r.HasValue ? r.Value.ToString().ToLowerInvariant() : null,
                    v => v == null ? null : Enum.Parse<ChallengeEndReason>(v, true));
            e.Ignore(c => c.IsEnded);
            e.Ignore(c => c.ResendAvailableAt);
            // "Newest challenge for this address" (the 60-second rule, FR-AUTH-033).
            e.HasIndex(c => new { c.EmailHash, c.CreatedAt }).IsDescending(false, true);
            // One open challenge per address: closes the race between two parallel requests (design §3.2).
            e.HasIndex(c => c.EmailHash).IsUnique().HasFilter("ended_at is null").HasDatabaseName("ix_sign_in_code_challenges_open");
            e.HasIndex(c => c.ExpiresAt);
            // Row version: parallel submissions of one challenge conflict and are retried (design §3.3).
            e.Property<uint>("Version").IsRowVersion();
            e.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.AddOutboxAndInbox();
        modelBuilder.UseOpenIddict();
    }
}
