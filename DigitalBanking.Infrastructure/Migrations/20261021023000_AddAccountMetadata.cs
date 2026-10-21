using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing accounts are preserved and become Vadesiz. The defaults
            // make this additive migration safe for an operator-managed upgrade.
            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Vadesiz Hesap");

            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RequestedAccountName",
                table: "PaymentApprovals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedAccountType",
                table: "PaymentApprovals",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RequestedAccountType", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "RequestedAccountName", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "AccountType", table: "Accounts");
            migrationBuilder.DropColumn(name: "AccountName", table: "Accounts");
        }
    }
}
