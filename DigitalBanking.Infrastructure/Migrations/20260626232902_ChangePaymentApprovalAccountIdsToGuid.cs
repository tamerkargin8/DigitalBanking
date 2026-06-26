using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangePaymentApprovalAccountIdsToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
    name: "FromAccountId",
    table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "PaymentApprovals");

            migrationBuilder.AddColumn<Guid>(
                name: "FromAccountId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "ToAccountId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromAccountId",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "PaymentApprovals");

            migrationBuilder.AddColumn<int>(
                name: "FromAccountId",
                table: "PaymentApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ToAccountId",
                table: "PaymentApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
