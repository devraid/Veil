using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veil.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameChatFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserText",
                table: "Chat");

            migrationBuilder.RenameColumn(
                name: "Answer",
                table: "Chat",
                newName: "Content");

            migrationBuilder.AddColumn<Guid>(
                name: "ChatId",
                table: "Chat",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Chat",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Chat_ChatId_Timestamp",
                table: "Chat",
                columns: new[] { "ChatId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chat_ChatId_Timestamp",
                table: "Chat");

            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "Chat");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Chat");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Chat",
                newName: "Answer");

            migrationBuilder.AddColumn<string>(
                name: "UserText",
                table: "Chat",
                type: "TEXT",
                nullable: true);
        }
    }
}
