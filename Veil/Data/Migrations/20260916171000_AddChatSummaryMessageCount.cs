using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Veil.Data;

#nullable disable

namespace Veil.Data.Migrations
{
    [DbContext(typeof(VeilDbContext))]
    [Migration("20260916171000_AddChatSummaryMessageCount")]
    public partial class AddChatSummaryMessageCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SummaryMessageCount",
                table: "Chat",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryMessageCount",
                table: "Chat");
        }
    }
}