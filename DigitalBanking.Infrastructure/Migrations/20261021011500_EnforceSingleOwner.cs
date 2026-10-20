using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stop before creating the index if an older/raced database already has
            // more than one Owner. No row is deleted or demoted; an operator must
            // choose the surviving Owner explicitly.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [Users] WHERE [Role] = 3 GROUP BY [Role] HAVING COUNT(*) > 1) " +
                "THROW 51000, N'Multiple Owners exist. No data was changed. " +
                "An operator must decide which account keeps Role=Owner before applying " +
                "20261021011500_EnforceSingleOwner.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_OwnerUnique",
                table: "Users",
                column: "Role",
                unique: true,
                filter: "[Role] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Role_OwnerUnique",
                table: "Users");
        }
    }
}
