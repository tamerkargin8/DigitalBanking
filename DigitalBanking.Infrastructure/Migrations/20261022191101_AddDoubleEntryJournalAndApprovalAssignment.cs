using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleEntryJournalAndApprovalAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "PaymentApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToUsername",
                table: "PaymentApprovals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceOperation = table.Column<int>(type: "int", nullable: false),
                    PaymentApprovalId = table.Column<int>(type: "int", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    IsBaseline = table.Column<bool>(type: "bit", nullable: false),
                    ReversalOfJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentApprovals_AssignedToUserId_Status",
                table: "PaymentApprovals",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CorrelationId",
                table: "JournalEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Currency_CreatedAt",
                table: "JournalEntries",
                columns: new[] { "Currency", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PaymentApprovalId",
                table: "JournalEntries",
                column: "PaymentApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceKey",
                table: "JournalEntries",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_AccountId",
                table: "JournalLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_CorrelationId",
                table: "JournalLines",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_JournalEntryId_Side",
                table: "JournalLines",
                columns: new[] { "JournalEntryId", "Side" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_TransactionId",
                table: "JournalLines",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalLines");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_PaymentApprovals_AssignedToUserId_Status",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "AssignedToUsername",
                table: "PaymentApprovals");
        }
    }
}
