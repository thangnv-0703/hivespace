using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "display_name",
            table: "users",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(60)",
            oldMaxLength: 60);

        migrationBuilder.AddColumn<DateOnly>(
            name: "date_of_birth",
            table: "users",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "gender",
            table: "users",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "phone",
            table: "users",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_users_gender",
            table: "users",
            sql: "gender in ('male', 'female', 'other', 'prefer_not_to_say')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_users_gender",
            table: "users");

        migrationBuilder.DropColumn(
            name: "date_of_birth",
            table: "users");

        migrationBuilder.DropColumn(
            name: "gender",
            table: "users");

        migrationBuilder.DropColumn(
            name: "phone",
            table: "users");

        // The data is not truncated: a name longer than 60 characters makes Postgres reject the narrowing,
        // so a rollback after long names exist fails loudly instead of losing data (technical-design §4).
        migrationBuilder.AlterColumn<string>(
            name: "display_name",
            table: "users",
            type: "character varying(60)",
            maxLength: 60,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80);
    }
}
