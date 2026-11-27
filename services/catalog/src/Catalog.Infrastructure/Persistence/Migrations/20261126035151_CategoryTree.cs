using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CategoryTree : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A uuid key cannot be altered to an integer, so the skeleton table and its seed row are dropped
        // and recreated (FEAT-014 design §4). Catalog has no released data.
        migrationBuilder.DropTable(
            name: "categories");

        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                ParentId = table.Column<int>(type: "integer", nullable: true),
                Position = table.Column<int>(type: "integer", nullable: false),
                IsLeaf = table.Column<bool>(type: "boolean", nullable: false),
                ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.Id);
                table.CheckConstraint("CK_categories_ImageUrl_TopLevel", "\"ImageUrl\" IS NULL OR \"ParentId\" IS NULL");
                table.ForeignKey(
                    name: "FK_categories_categories_ParentId",
                    column: x => x.ParentId,
                    principalTable: "categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "attributes",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attributes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "attribute_values",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                AttributeId = table.Column<int>(type: "integer", nullable: false),
                NameEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                NameVi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attribute_values", x => x.Id);
                table.ForeignKey(
                    name: "FK_attribute_values_attributes_AttributeId",
                    column: x => x.AttributeId,
                    principalTable: "attributes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "category_attributes",
            columns: table => new
            {
                CategoryId = table.Column<int>(type: "integer", nullable: false),
                AttributeId = table.Column<int>(type: "integer", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                Kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                AllowsMultiple = table.Column<bool>(type: "boolean", nullable: false),
                MaxValues = table.Column<int>(type: "integer", nullable: false),
                ValueIds = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "'{}'")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_category_attributes", x => new { x.CategoryId, x.AttributeId });
                table.CheckConstraint("CK_category_attributes_Kind", "\"Kind\" IN ('list', 'text')");
                table.CheckConstraint("CK_category_attributes_MaxValues", "(\"Kind\" = 'text' AND \"MaxValues\" = 1 AND NOT \"AllowsMultiple\") OR (\"Kind\" = 'list' AND ((\"AllowsMultiple\" AND \"MaxValues\" >= 2) OR (NOT \"AllowsMultiple\" AND \"MaxValues\" = 1)))");
                table.ForeignKey(
                    name: "FK_category_attributes_attributes_AttributeId",
                    column: x => x.AttributeId,
                    principalTable: "attributes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_category_attributes_categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_categories_ParentId_Position",
            table: "categories",
            columns: new[] { "ParentId", "Position" });

        migrationBuilder.CreateIndex(
            name: "IX_attribute_values_AttributeId",
            table: "attribute_values",
            column: "AttributeId");

        migrationBuilder.CreateIndex(
            name: "IX_category_attributes_AttributeId",
            table: "category_attributes",
            column: "AttributeId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "attribute_values");

        migrationBuilder.DropTable(
            name: "category_attributes");

        migrationBuilder.DropTable(
            name: "attributes");

        migrationBuilder.DropTable(
            name: "categories");

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

        migrationBuilder.InsertData(
            table: "categories",
            columns: new[] { "Id", "NameEn", "NameVi", "Slug" },
            values: new object[] { new Guid("01930000-0000-7000-8000-000000000001"), "Fashion", "Thời trang", "thoi-trang" });

        migrationBuilder.CreateIndex(
            name: "IX_categories_Slug",
            table: "categories",
            column: "Slug",
            unique: true);
    }
}
