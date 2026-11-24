using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSignInCodeChallenges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_entries_method",
            table: "audit_entries");

        migrationBuilder.CreateTable(
            name: "sign_in_code_challenges",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                end_reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sign_in_code_challenges", x => x.id);
                table.CheckConstraint("ck_sign_in_code_challenges_end_reason", "end_reason in ('used','superseded','attempts')");
                table.CheckConstraint("ck_sign_in_code_challenges_ended", "(ended_at is null) = (end_reason is null)");
                table.CheckConstraint("ck_sign_in_code_challenges_failed_count", "failed_count between 0 and 5");
                table.ForeignKey(
                    name: "FK_sign_in_code_challenges_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_entries_method",
            table: "audit_entries",
            sql: "method in ('password','google','password_change','code')");

        migrationBuilder.CreateIndex(
            name: "IX_sign_in_code_challenges_email_hash_created_at",
            table: "sign_in_code_challenges",
            columns: new[] { "email_hash", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_sign_in_code_challenges_expires_at",
            table: "sign_in_code_challenges",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_sign_in_code_challenges_open",
            table: "sign_in_code_challenges",
            column: "email_hash",
            unique: true,
            filter: "ended_at is null");

        migrationBuilder.CreateIndex(
            name: "IX_sign_in_code_challenges_user_id",
            table: "sign_in_code_challenges",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "sign_in_code_challenges");

        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_entries_method",
            table: "audit_entries");

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_entries_method",
            table: "audit_entries",
            sql: "method in ('password','google','password_change')");
    }
}
