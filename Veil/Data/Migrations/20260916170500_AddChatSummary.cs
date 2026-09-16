using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Veil.Data;

#nullable disable

namespace Veil.Data.Migrations
{
    [DbContext(typeof(VeilDbContext))]
    [Migration("20260916170500_AddChatSummary")]
    public partial class AddChatSummary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Chat",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Chat");
        }
    }
}