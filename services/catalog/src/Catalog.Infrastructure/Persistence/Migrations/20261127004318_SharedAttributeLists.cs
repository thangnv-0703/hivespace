using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SharedAttributeLists : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The tree tables hold at most the sample or test data (no products exist yet). Per-link lists cannot be merged
        // into one list per attribute faithfully in SQL, so they are emptied and the operator reloads the v2 file (design §4.1).
        migrationBuilder.Sql("TRUNCATE category_attributes, attribute_values, attributes, categories");

        migrationBuilder.DropCheckConstraint(
            name: "CK_category_attributes_Kind",
            table: "category_attributes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_category_attributes_MaxValues",
            table: "category_attributes");

        migrationBuilder.DropIndex(
            name: "IX_attribute_values_AttributeId",
            table: "attribute_values");

        migrationBuilder.DropColumn(
            name: "AllowsMultiple",
            table: "category_attributes");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "category_attributes");

        migrationBuilder.DropColumn(
            name: "MaxValues",
            table: "category_attributes");

        migrationBuilder.DropColumn(
            name: "ValueIds",
            table: "category_attributes");

        migrationBuilder.AddColumn<bool>(
            name: "AllowsCustomValue",
            table: "attributes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "AllowsMultiple",
            table: "attributes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "attributes",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "MaxValues",
            table: "attributes",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "Position",
            table: "attribute_values",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_attributes_AllowsCustomValue",
            table: "attributes",
            sql: "NOT \"AllowsCustomValue\" OR \"Kind\" = 'list'");

        migrationBuilder.AddCheckConstraint(
            name: "CK_attributes_Kind",
            table: "attributes",
            sql: "\"Kind\" IN ('list', 'text')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_attributes_MaxValues",
            table: "attributes",
            sql: "(\"Kind\" = 'text' AND \"MaxValues\" = 1 AND NOT \"AllowsMultiple\") OR (\"Kind\" = 'list' AND ((\"AllowsMultiple\" AND \"MaxValues\" >= 2) OR (NOT \"AllowsMultiple\" AND \"MaxValues\" = 1)))");

        migrationBuilder.CreateIndex(
            name: "IX_attribute_values_AttributeId_Position",
            table: "attribute_values",
            columns: new[] { "AttributeId", "Position" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Emptied again: the attribute-level lists cannot be spread back onto links (design §4.1).
        migrationBuilder.Sql("TRUNCATE category_attributes, attribute_values, attributes, categories");

        migrationBuilder.DropCheckConstraint(
            name: "CK_attributes_AllowsCustomValue",
            table: "attributes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_attributes_Kind",
            table: "attributes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_attributes_MaxValues",
            table: "attributes");

        migrationBuilder.DropIndex(
            name: "IX_attribute_values_AttributeId_Position",
            table: "attribute_values");

        migrationBuilder.DropColumn(
            name: "AllowsCustomValue",
            table: "attributes");

        migrationBuilder.DropColumn(
            name: "AllowsMultiple",
            table: "attributes");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "attributes");

        migrationBuilder.DropColumn(
            name: "MaxValues",
            table: "attributes");

        migrationBuilder.DropColumn(
            name: "Position",
            table: "attribute_values");

        migrationBuilder.AddColumn<bool>(
            name: "AllowsMultiple",
            table: "category_attributes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "category_attributes",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "MaxValues",
            table: "category_attributes",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<List<int>>(
            name: "ValueIds",
            table: "category_attributes",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddCheckConstraint(
            name: "CK_category_attributes_Kind",
            table: "category_attributes",
            sql: "\"Kind\" IN ('list', 'text')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_category_attributes_MaxValues",
            table: "category_attributes",
            sql: "(\"Kind\" = 'text' AND \"MaxValues\" = 1 AND NOT \"AllowsMultiple\") OR (\"Kind\" = 'list' AND ((\"AllowsMultiple\" AND \"MaxValues\" >= 2) OR (NOT \"AllowsMultiple\" AND \"MaxValues\" = 1)))");

        migrationBuilder.CreateIndex(
            name: "IX_attribute_values_AttributeId",
            table: "attribute_values",
            column: "AttributeId");
    }
}
