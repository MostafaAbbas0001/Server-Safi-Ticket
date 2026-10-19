using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safi_Ticket.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CcRecipients",
                table: "TicketComments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "ToRecipients",
                table: "TicketComments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CcRecipients",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "ToRecipients",
                table: "TicketComments");
        }
    }
}
