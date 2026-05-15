using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamDynamicsAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixUserProfileFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMessages");

            migrationBuilder.DropTable(
                name: "AiSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSessions",
                columns: table => new
                {
                    AiSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSessions", x => x.AiSessionId);
                    table.ForeignKey(
                        name: "FK_AiSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    AiMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AiSessionId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.AiMessageId);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiSessions_AiSessionId",
                        column: x => x.AiSessionId,
                        principalTable: "AiSessions",
                        principalColumn: "AiSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_AiSessionId",
                table: "AiMessages",
                column: "AiSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSessions_UserId",
                table: "AiSessions",
                column: "UserId");
        }
    }
}
