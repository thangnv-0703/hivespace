using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "shops",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "payout_account_ciphertext",
                table: "shops",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payout_account_holder",
                table: "shops",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payout_account_last4",
                table: "shops",
                type: "char(4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payout_bank_name",
                table: "shops",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payout_key_id",
                table: "shops",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_contact_name",
                table: "shops",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_district",
                table: "shops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_phone",
                table: "shops",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_province",
                table: "shops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_street",
                table: "shops",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_ward",
                table: "shops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "profile_version",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // xmin is Postgres's system column, the concurrency token (FEAT-010 D-9): it exists on every table, so it is not created here.

            migrationBuilder.AddCheckConstraint(
                name: "ck_shops_payout_all_or_none",
                table: "shops",
                sql: "(payout_bank_name is null) = (payout_account_holder is null) and (payout_bank_name is null) = (payout_account_ciphertext is null) and (payout_bank_name is null) = (payout_account_last4 is null) and (payout_bank_name is null) = (payout_key_id is null)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shops_pickup_all_or_none",
                table: "shops",
                sql: "(pickup_contact_name is null) = (pickup_phone is null) and (pickup_contact_name is null) = (pickup_province is null) and (pickup_contact_name is null) = (pickup_district is null) and (pickup_contact_name is null) = (pickup_ward is null) and (pickup_contact_name is null) = (pickup_street is null)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back drops the shops' descriptions, pickup addresses and payout details (FEAT-010 §4).
            migrationBuilder.DropCheckConstraint(
                name: "ck_shops_payout_all_or_none",
                table: "shops");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shops_pickup_all_or_none",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "description",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "payout_account_ciphertext",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "payout_account_holder",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "payout_account_last4",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "payout_bank_name",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "payout_key_id",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_contact_name",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_district",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_phone",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_province",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_street",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pickup_ward",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "profile_version",
                table: "shops");
        }
    }
}
