using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExternalLogins : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "external_logins",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_external_logins", x => x.id);
                table.ForeignKey(
                    name: "FK_external_logins_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_provider_subject",
            table: "external_logins",
            columns: new[] { "provider", "subject" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_user_id_provider",
            table: "external_logins",
            columns: new[] { "user_id", "provider" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_logins");
    }
}
