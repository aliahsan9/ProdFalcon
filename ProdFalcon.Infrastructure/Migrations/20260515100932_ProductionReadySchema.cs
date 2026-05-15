using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductionReadySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ScanIssues_ScanSessions_ScanSessionId",
                table: "ScanIssues");

            migrationBuilder.DropTable(
                name: "ScanSessions");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_ScanSessionId",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "ScanSessionId",
                table: "ScanIssues");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "DurationMs",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaintainabilityScore",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PerformanceScore",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductionReadinessScore",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ScanProjectId",
                table: "ScanResults",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SecurityScore",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ScanResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "ScanIssues",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "ScanResultId",
                table: "ScanIssues",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RuleName",
                table: "ScanIssues",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ScanIssues",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ScanProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ZipPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ExtractedPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_CreatedAt",
                table: "ScanResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_ScanProjectId",
                table: "ScanResults",
                column: "ScanProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_RuleName",
                table: "ScanIssues",
                column: "RuleName");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_Severity",
                table: "ScanIssues",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ScanProjects_UploadedAt",
                table: "ScanProjects",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_StripeCustomerId",
                table: "Subscriptions",
                column: "StripeCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues",
                column: "ScanResultId",
                principalTable: "ScanResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScanResults_ScanProjects_ScanProjectId",
                table: "ScanResults",
                column: "ScanProjectId",
                principalTable: "ScanProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ScanResults_ScanProjects_ScanProjectId",
                table: "ScanResults");

            migrationBuilder.DropTable(
                name: "ScanProjects");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ScanResults_CreatedAt",
                table: "ScanResults");

            migrationBuilder.DropIndex(
                name: "IX_ScanResults_ScanProjectId",
                table: "ScanResults");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_RuleName",
                table: "ScanIssues");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_Severity",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "MaintainabilityScore",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "PerformanceScore",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "ProductionReadinessScore",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "ScanProjectId",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "SecurityScore",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ScanIssues");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "ScanIssues",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<int>(
                name: "ScanResultId",
                table: "ScanIssues",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "RuleName",
                table: "ScanIssues",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<int>(
                name: "ScanSessionId",
                table: "ScanIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ScanSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExtractedPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_ScanSessionId",
                table: "ScanIssues",
                column: "ScanSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues",
                column: "ScanResultId",
                principalTable: "ScanResults",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ScanIssues_ScanSessions_ScanSessionId",
                table: "ScanIssues",
                column: "ScanSessionId",
                principalTable: "ScanSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
