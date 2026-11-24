using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddShops : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_entries_type",
            table: "audit_entries");

        migrationBuilder.AlterColumn<string>(
            name: "method",
            table: "audit_entries",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16);

        migrationBuilder.CreateTable(
            name: "shops",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                seller_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_shops", x => x.id);
                table.CheckConstraint("ck_shops_seller_type", "seller_type in ('individual','business')");
                table.ForeignKey(
                    name: "FK_shops_users_owner_user_id",
                    column: x => x.owner_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_entries_type",
            table: "audit_entries",
            sql: "type in ('sign_in_succeeded','sign_in_failed','account_locked','seller_role_granted')");

        migrationBuilder.CreateIndex(
            name: "ix_shops_normalized_name",
            table: "shops",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_shops_owner_user_id",
            table: "shops",
            column: "owner_user_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rolling back removes the seller state together with the shops: the Seller grants and their audit rows go too (design §4).
        migrationBuilder.DropTable(
            name: "shops");

        migrationBuilder.Sql("DELETE FROM audit_entries WHERE type = 'seller_role_granted' OR method IS NULL;");
        migrationBuilder.Sql("DELETE FROM role_grants WHERE role = 'Seller';");

        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_entries_type",
            table: "audit_entries");

        migrationBuilder.Sql("ALTER TABLE audit_entries ALTER COLUMN method SET NOT NULL;");

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_entries_type",
            table: "audit_entries",
            sql: "type in ('sign_in_succeeded','sign_in_failed','account_locked')");
    }
}
