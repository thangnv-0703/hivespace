using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "inbox",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                Consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox", x => new { x.EventId, x.Consumer });
            });

        migrationBuilder.CreateTable(
            name: "outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Envelope = table.Column<string>(type: "jsonb", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox", x => x.Id);
            });

        migrationBuilder.InsertData(
            table: "categories",
            columns: new[] { "Id", "NameEn", "NameVi", "Slug" },
            values: new object[] { new Guid("01930000-0000-7000-8000-000000000001"), "Fashion", "Thời trang", "thoi-trang" });

        migrationBuilder.CreateIndex(
            name: "IX_categories_Slug",
            table: "categories",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_SentAt",
            table: "outbox",
            column: "SentAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "categories");

        migrationBuilder.DropTable(
            name: "inbox");

        migrationBuilder.DropTable(
            name: "outbox");
    }
}
