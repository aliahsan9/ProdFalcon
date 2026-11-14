using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColumnNumber",
                table: "ScanIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CollaborationNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanResultId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubAccountConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GitHubLogin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GitHubUserId = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubAccountConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestScans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HeadSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScanResultId = table.Column<int>(type: "int", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestScans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemediationProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanResultId = table.Column<int>(type: "int", nullable: false),
                    ScanIssueId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ColumnNumber = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SecureSuggestion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OriginalSnippet = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PatchedSnippet = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DiffText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UsedExternalModel = table.Column<bool>(type: "bit", nullable: false),
                    Grounding = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationProposals_ScanResults_ScanResultId",
                        column: x => x.ScanResultId,
                        principalTable: "ScanResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecurityPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    FailOnCritical = table.Column<bool>(type: "bit", nullable: false),
                    FailOnHigh = table.Column<bool>(type: "bit", nullable: false),
                    MaxOpenCritical = table.Column<int>(type: "int", nullable: false),
                    MaxOpenHigh = table.Column<int>(type: "int", nullable: false),
                    MinOverallScore = table.Column<int>(type: "int", nullable: false),
                    MinSecurityScore = table.Column<int>(type: "int", nullable: false),
                    PreventScoreRegression = table.Column<bool>(type: "bit", nullable: false),
                    FailOnNewSecrets = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationNotes_ScanResultId",
                table: "CollaborationNotes",
                column: "ScanResultId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationNotes_TenantId",
                table: "CollaborationNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubAccountConnections_TenantId",
                table: "GitHubAccountConnections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubAccountConnections_TenantId_UserId",
                table: "GitHubAccountConnections",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestScans_CreatedAt",
                table: "PullRequestScans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestScans_TenantId",
                table: "PullRequestScans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationProposals_ScanResultId_ScanIssueId",
                table: "RemediationProposals",
                columns: new[] { "ScanResultId", "ScanIssueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemediationProposals_TenantId",
                table: "RemediationProposals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityPolicies_TenantId",
                table: "SecurityPolicies",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationNotes");

            migrationBuilder.DropTable(
                name: "GitHubAccountConnections");

            migrationBuilder.DropTable(
                name: "PullRequestScans");

            migrationBuilder.DropTable(
                name: "RemediationProposals");

            migrationBuilder.DropTable(
                name: "SecurityPolicies");

            migrationBuilder.DropColumn(
                name: "ColumnNumber",
                table: "ScanIssues");
        }
    }
}
