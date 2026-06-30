using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safi_Ticket.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsDeleted_CreatedAt",
                table: "Tickets",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsDeleted_PriorityId",
                table: "Tickets",
                columns: new[] { "IsDeleted", "PriorityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsDeleted_StatusId",
                table: "Tickets",
                columns: new[] { "IsDeleted", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IsDeleted_UserId",
                table: "Tickets",
                columns: new[] { "IsDeleted", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequesterEmail",
                table: "Tickets",
                column: "RequesterEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_TicketId_CreatedAt",
                table: "TicketComments",
                columns: new[] { "TicketId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketComments_TicketId_CreatedAt",
                table: "TicketComments");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_RequesterEmail",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsDeleted_UserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsDeleted_StatusId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsDeleted_PriorityId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IsDeleted_CreatedAt",
                table: "Tickets");
        }
    }
}
