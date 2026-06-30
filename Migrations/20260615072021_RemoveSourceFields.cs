using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safi_Ticket.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TicketComments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TicketComments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
