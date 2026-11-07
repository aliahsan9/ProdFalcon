using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProdFalcon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedRepositoryRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepositoryRegistrations_Owner_Name",
                table: "RepositoryRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryRegistrations_TenantId_Owner_Name",
                table: "RepositoryRegistrations",
                columns: new[] { "TenantId", "Owner", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepositoryRegistrations_TenantId_Owner_Name",
                table: "RepositoryRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryRegistrations_Owner_Name",
                table: "RepositoryRegistrations",
                columns: new[] { "Owner", "Name" },
                unique: true);
        }
    }
}
