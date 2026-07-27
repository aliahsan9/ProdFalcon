using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        private static readonly Guid DefaultTenantId = new("11111111-1111-1111-1111-111111111111");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    StorageUsed = table.Column<long>(type: "bigint", nullable: false),
                    StorageLimit = table.Column<long>(type: "bigint", nullable: false),
                    ScanLimit = table.Column<int>(type: "int", nullable: false),
                    AIUsage = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InviteToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ScanResults",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ScanProjects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ScanIssues",
                type: "uniqueidentifier",
                nullable: true);

            // Seed default tenant for orphaned historical data.
            // [Plan] must be bracketed — Plan is a reserved keyword in SQL Server.
            migrationBuilder.Sql($"""
                INSERT INTO Tenants (Id, Name, Slug, OwnerUserId, CreatedAt, UpdatedAt, Status, [Plan], StorageUsed, StorageLimit, ScanLimit, AIUsage, IsDeleted)
                VALUES ('{DefaultTenantId}', N'Default', N'default', NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), 0, 0, 0, 1073741824, 5, 0, 0);
                """);

            // Create a personal tenant + Owner membership (+ subscription) for each existing user.
            migrationBuilder.Sql("""
                DECLARE @UserId INT, @FullName NVARCHAR(512), @Email NVARCHAR(512), @TenantId UNIQUEIDENTIFIER, @Slug NVARCHAR(128);

                DECLARE user_cursor CURSOR LOCAL FAST_FORWARD FOR
                SELECT Id, FullName, Email FROM Users ORDER BY Id;

                OPEN user_cursor;
                FETCH NEXT FROM user_cursor INTO @UserId, @FullName, @Email;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @TenantId = NEWID();
                    SET @Slug = LOWER(REPLACE(REPLACE(REPLACE(ISNULL(NULLIF(@FullName, ''), @Email), ' ', '-'), '''', ''), '.', '-')) + '-' + CAST(@UserId AS NVARCHAR(20));

                    INSERT INTO Tenants (Id, Name, Slug, OwnerUserId, CreatedAt, UpdatedAt, Status, [Plan], StorageUsed, StorageLimit, ScanLimit, AIUsage, IsDeleted)
                    VALUES (@TenantId, CONCAT(ISNULL(NULLIF(@FullName, ''), @Email), N'''s Workspace'), @Slug, @UserId, SYSUTCDATETIME(), SYSUTCDATETIME(), 0, 0, 0, 1073741824, 5, 0, 0);

                    INSERT INTO TenantMembers (TenantId, UserId, Role, Status, InvitedAt, JoinedAt, InviteToken, InviteExpiresAt)
                    VALUES (@TenantId, @UserId, 4, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL);

                    IF EXISTS (SELECT 1 FROM Subscriptions WHERE UserId = @UserId)
                        UPDATE Subscriptions SET TenantId = @TenantId WHERE UserId = @UserId AND TenantId IS NULL;
                    ELSE
                        INSERT INTO Subscriptions (UserId, Tier, StripeCustomerId, StripeSubscriptionId, IsActive, ExpiresAt, CreatedAt, TenantId)
                        VALUES (@UserId, 0, N'', N'', 1, NULL, SYSUTCDATETIME(), @TenantId);

                    UPDATE ScanProjects SET TenantId = @TenantId WHERE UserId = @UserId AND TenantId IS NULL;

                    FETCH NEXT FROM user_cursor INTO @UserId, @FullName, @Email;
                END

                CLOSE user_cursor;
                DEALLOCATE user_cursor;
                """);

            // Orphan projects/results/issues/subscriptions → default tenant.
            migrationBuilder.Sql($"""
                UPDATE ScanProjects SET TenantId = '{DefaultTenantId}' WHERE TenantId IS NULL;
                UPDATE ScanResults SET TenantId = p.TenantId
                    FROM ScanResults r INNER JOIN ScanProjects p ON r.ScanProjectId = p.Id
                    WHERE r.TenantId IS NULL;
                UPDATE ScanResults SET TenantId = '{DefaultTenantId}' WHERE TenantId IS NULL;
                UPDATE ScanIssues SET TenantId = r.TenantId
                    FROM ScanIssues i INNER JOIN ScanResults r ON i.ScanResultId = r.Id
                    WHERE i.TenantId IS NULL;
                UPDATE ScanIssues SET TenantId = '{DefaultTenantId}' WHERE TenantId IS NULL;
                UPDATE Subscriptions SET TenantId = '{DefaultTenantId}' WHERE TenantId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "ScanResults",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "ScanProjects",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "ScanIssues",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_TenantId",
                table: "ScanResults",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanProjects_TenantId",
                table: "ScanProjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIssues_TenantId",
                table: "ScanIssues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_InviteToken",
                table: "TenantMembers",
                column: "InviteToken");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_TenantId",
                table: "TenantMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_TenantId_UserId",
                table: "TenantMembers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_UserId",
                table: "TenantMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OwnerUserId",
                table: "Tenants",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "TenantMembers");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_ScanResults_TenantId",
                table: "ScanResults");

            migrationBuilder.DropIndex(
                name: "IX_ScanProjects_TenantId",
                table: "ScanProjects");

            migrationBuilder.DropIndex(
                name: "IX_ScanIssues_TenantId",
                table: "ScanIssues");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ScanResults");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ScanProjects");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ScanIssues");
        }
    }
}
