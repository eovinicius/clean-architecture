using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Harden_Refresh_Tokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plaintext tokens cannot become members of a family; existing sessions must log in again.
            migrationBuilder.Sql("DELETE FROM public.refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at_utc",
                schema: "public",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "public",
                table: "refresh_tokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                schema: "public",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_family_id",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_at_utc",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "token",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens",
                column: "token",
                unique: true);
        }
    }
}
