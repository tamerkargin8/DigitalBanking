using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOwnershipTimelineReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "PaymentApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Legacy approvals have no correlation id yet; give each a distinct one.
            migrationBuilder.Sql(
                "UPDATE [PaymentApprovals] SET [CorrelationId] = NEWID() " +
                "WHERE [CorrelationId] = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityNumber",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PaymentTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentApprovalId = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTimelineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyScope = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DirectionScope = table.Column<int>(type: "int", nullable: true),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    HasErrors = table.Column<bool>(type: "bit", nullable: false),
                    ValidationSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportedByUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    OnlyExternalCount = table.Column<int>(type: "int", nullable: false),
                    OnlySystemCount = table.Column<int>(type: "int", nullable: false),
                    AmountMismatchCount = table.Column<int>(type: "int", nullable: false),
                    CurrencyMismatchCount = table.Column<int>(type: "int", nullable: false),
                    DuplicateReferenceCount = table.Column<int>(type: "int", nullable: false),
                    PendingApprovalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ValidationError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MatchStatus = table.Column<int>(type: "int", nullable: false),
                    MatchedTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SystemAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SystemCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationRows_ReconciliationBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ReconciliationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProposedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedByUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProposedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationExceptions_ReconciliationBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ReconciliationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReconciliationExceptions_ReconciliationRows_RowId",
                        column: x => x.RowId,
                        principalTable: "ReconciliationRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CorrelationId",
                table: "Transactions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentApprovals_CorrelationId",
                table: "PaymentApprovals",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IdentityNumber",
                table: "Customers",
                column: "IdentityNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId");

            // SQL Server unique indexes allow only one NULL; make the ownership link
            // unique only for linked customers. The SQLite Development database is
            // created with EnsureCreated and enforces uniqueness in the service layer.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX [IX_Customers_UserId_Unique] ON [Customers]([UserId]) " +
                "WHERE [UserId] IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTimelineEvents_CorrelationId",
                table: "PaymentTimelineEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTimelineEvents_OccurredAtUtc",
                table: "PaymentTimelineEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTimelineEvents_PaymentApprovalId",
                table: "PaymentTimelineEvents",
                column: "PaymentApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_CreatedAt",
                table: "ReconciliationBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_FileHash_ScopeKey",
                table: "ReconciliationBatches",
                columns: new[] { "FileHash", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_Status_CreatedAt",
                table: "ReconciliationBatches",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_BatchId",
                table: "ReconciliationExceptions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_RowId",
                table: "ReconciliationExceptions",
                column: "RowId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_Status_UpdatedAt",
                table: "ReconciliationExceptions",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRows_BatchId",
                table: "ReconciliationRows",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRows_BatchId_ExternalReference_Direction",
                table: "ReconciliationRows",
                columns: new[] { "BatchId", "ExternalReference", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRows_MatchStatus",
                table: "ReconciliationRows",
                column: "MatchStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTimelineEvents");

            migrationBuilder.DropTable(
                name: "ReconciliationExceptions");

            migrationBuilder.DropTable(
                name: "ReconciliationRows");

            migrationBuilder.DropTable(
                name: "ReconciliationBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CorrelationId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentApprovals_CorrelationId",
                table: "PaymentApprovals");

            migrationBuilder.DropIndex(
                name: "IX_Customers_IdentityNumber",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId",
                table: "Customers");

            migrationBuilder.Sql("DROP INDEX [IX_Customers_UserId_Unique] ON [Customers];");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "PaymentApprovals");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityNumber",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
