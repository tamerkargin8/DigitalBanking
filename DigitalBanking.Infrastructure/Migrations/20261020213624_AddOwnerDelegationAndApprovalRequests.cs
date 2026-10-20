using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerDelegationAndApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivatedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "int",
                nullable: false,
                // Existing users were already able to log in; new registrations
                // explicitly insert PendingActivation. Legacy roles (1/2) remain
                // invalid until an operator maps them to Owner/Customer.
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedAccountId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "PaymentApprovals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Every pre-existing approval was a transfer in the old model.
            migrationBuilder.Sql("UPDATE [PaymentApprovals] SET [RequestType] = 1 WHERE [RequestType] = 0;");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedCustomerId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedInitialBalance",
                table: "PaymentApprovals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DailyLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByOwnerUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_Status",
                table: "Users",
                columns: new[] { "Role", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentApprovals_RequestType_Status",
                table: "PaymentApprovals",
                columns: new[] { "RequestType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_IsActive_ExpiresAt",
                table: "ApprovalDelegations",
                columns: new[] { "IsActive", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_UserId_Operation_Currency",
                table: "ApprovalDelegations",
                columns: new[] { "UserId", "Operation", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDelegations");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Role_Status",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PaymentApprovals_RequestType_Status",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAccountId",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "RequestedCustomerId",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "RequestedInitialBalance",
                table: "PaymentApprovals");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
