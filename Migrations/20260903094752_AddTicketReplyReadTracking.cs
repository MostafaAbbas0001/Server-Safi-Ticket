using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Safi_Ticket.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketReplyReadTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRequesterReplyAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "RequesterReplyVersion",
                table: "Tickets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            );

            migrationBuilder.CreateTable(
                name: "TicketReplyReadStates",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    SeenRequesterReplyVersion = table.Column<long>(type: "bigint", nullable: false),
                    LastReadAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketReplyReadStates", x => new { x.UserId, x.TicketId });
                    table.ForeignKey(
                        name: "FK_TicketReplyReadStates_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TicketReplyReadStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TicketReplyReadStates_TicketId",
                table: "TicketReplyReadStates",
                column: "TicketId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TicketReplyReadStates");

            migrationBuilder.DropColumn(name: "LastRequesterReplyAt", table: "Tickets");

            migrationBuilder.DropColumn(name: "RequesterReplyVersion", table: "Tickets");
        }
    }
}
