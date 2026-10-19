using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safi_Ticket.Migrations
{
    /// <inheritdoc />
    public partial class AddInlineAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentId",
                table: "TicketAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInline",
                table: "TicketAttachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentId",
                table: "TicketAttachments");

            migrationBuilder.DropColumn(
                name: "IsInline",
                table: "TicketAttachments");
        }
    }
}
