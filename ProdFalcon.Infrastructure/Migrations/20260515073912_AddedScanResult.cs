using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedScanResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ScanIssues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "ScanIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScanResultId",
                table: "ScanIssues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScanResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_ScanResultId",
                table: "ScanIssues",
                column: "ScanResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues",
                column: "ScanResultId",
                principalTable: "ScanResults",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanIssues_ScanResults_ScanResultId",
                table: "ScanIssues");

            migrationBuilder.DropTable(
                name: "ScanResults");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_ScanResultId",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "ScanResultId",
                table: "ScanIssues");
        }
    }
}
