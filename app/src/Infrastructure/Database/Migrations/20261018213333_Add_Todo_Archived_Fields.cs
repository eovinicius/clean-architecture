using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Todo_Archived_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                schema: "public",
                table: "todo_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                schema: "public",
                table: "todo_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "public",
                table: "todo_items");

            migrationBuilder.DropColumn(
                name: "is_archived",
                schema: "public",
                table: "todo_items");
        }
    }
}
