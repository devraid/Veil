using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veil.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Chat",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Chat");
        }
    }
}
