using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SecurityPlatformExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ScanResults",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisDocument",
                table: "ScanResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "ScanResults",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                table: "ScanResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "ScanResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                table: "ScanResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreviousScore",
                table: "ScanResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "ScanResults",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "ScanResults",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StageMessage",
                table: "ScanResults",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Technologies",
                table: "ScanResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "ScanProjects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "ScanProjects",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedTechnologies",
                table: "ScanProjects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "ScanProjects",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryUrl",
                table: "ScanProjects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ScanProjects",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "ScanIssues",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Confidence",
                table: "ScanIssues",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Cve",
                table: "ScanIssues",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cwe",
                table: "ScanIssues",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionMethod",
                table: "ScanIssues",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "ScanIssues",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "ScanIssues",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Impact",
                table: "ScanIssues",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ScanIssues",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "References",
                table: "ScanIssues",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remediation",
                table: "ScanIssues",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecureExample",
                table: "ScanIssues",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ScanIssues",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CopilotMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanResultId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopilotMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FindingStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanIssueId = table.Column<int>(type: "int", nullable: false),
                    TrackedFindingId = table.Column<int>(type: "int", nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FindingStatusHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TokenPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FailOnNewCritical = table.Column<bool>(type: "bit", nullable: false),
                    WarnOnHigh = table.Column<bool>(type: "bit", nullable: false),
                    AllowAcceptedRisk = table.Column<bool>(type: "bit", nullable: false),
                    PreventScoreRegression = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    FirstScanResultId = table.Column<int>(type: "int", nullable: false),
                    LastScanResultId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedFindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_Status",
                table: "ScanResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_Fingerprint",
                table: "ScanIssues",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_Status",
                table: "ScanIssues",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CopilotMessages_ScanResultId",
                table: "CopilotMessages",
                column: "ScanResultId");

            migrationBuilder.CreateIndex(
                name: "IX_CopilotMessages_TenantId",
                table: "CopilotMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FindingStatusHistory_ScanIssueId",
                table: "FindingStatusHistory",
                column: "ScanIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_FindingStatusHistory_TenantId",
                table: "FindingStatusHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryRegistrations_Owner_Name",
                table: "RepositoryRegistrations",
                columns: new[] { "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryRegistrations_TenantId",
                table: "RepositoryRegistrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryRegistrations_TokenHash",
                table: "RepositoryRegistrations",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFindings_ScanProjectId_Fingerprint",
                table: "TrackedFindings",
                columns: new[] { "ScanProjectId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFindings_Status",
                table: "TrackedFindings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFindings_TenantId",
                table: "TrackedFindings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopilotMessages");

            migrationBuilder.DropTable(
                name: "FindingStatusHistory");

            migrationBuilder.DropTable(
                name: "RepositoryRegistrations");

            migrationBuilder.DropTable(
                name: "TrackedFindings");

            migrationBuilder.DropIndex(
                name: "IX_ScanResults_Status",
                table: "ScanResults");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_Fingerprint",
                table: "ScanIssues");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_Status",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "AnalysisDocument",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "ConfigurationJson",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "FileCount",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "PreviousScore",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "StageMessage",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "Technologies",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "DetectedTechnologies",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "RepositoryUrl",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Cve",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Cwe",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "DetectionMethod",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Impact",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "References",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Remediation",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "SecureExample",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ScanIssues");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ScanResults",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "ScanIssues",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);
        }
    }
}
