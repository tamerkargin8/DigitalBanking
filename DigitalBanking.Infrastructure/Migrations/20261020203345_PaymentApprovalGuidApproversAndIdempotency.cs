using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <summary>
    /// Moves PaymentApproval requester/reviewer ids from int to Guid (User.Id) and
    /// RelatedTransactionId from int to Guid (Transaction.Id), adds the rowversion
    /// concurrency token and the durable idempotency table.
    ///
    /// Existing SQL Server rows cannot be converted automatically: old int user ids
    /// were never linked to the Guid Users table. This migration therefore:
    ///   1. copies the old int values into LegacyPaymentApprovalIds for operator
    ///      reconciliation (no data is silently discarded);
    ///   2. backfills the new requester/reviewer ids with the earliest Admin user
    ///      (or the empty Guid when no Admin exists);
    ///   3. sets RelatedTransactionId to NULL because old int transaction ids cannot
    ///      be mapped to Guid transaction ids.
    ///
    /// Operators with real data must review LegacyPaymentApprovalIds and update the
    /// new columns before relying on approval history. See docs/migrations/README.md.
    /// This migration is SQL Server specific; the Development SQLite database is
    /// created with EnsureCreated and never applies these migrations.
    /// </summary>
    public partial class PaymentApprovalGuidApproversAndIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve the old numeric ids before any column is dropped.
            migrationBuilder.CreateTable(
                name: "LegacyPaymentApprovalIds",
                columns: table => new
                {
                    ApprovalId = table.Column<int>(type: "int", nullable: false),
                    OldRequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    OldReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    OldRelatedTransactionId = table.Column<int>(type: "int", nullable: true),
                    MigratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_LegacyPaymentApprovalIds", x => x.ApprovalId));

            migrationBuilder.Sql(
                "INSERT INTO [LegacyPaymentApprovalIds] " +
                "([ApprovalId], [OldRequestedByUserId], [OldReviewedByUserId], [OldRelatedTransactionId], [MigratedAt]) " +
                "SELECT [Id], [RequestedByUserId], [ReviewedByUserId], [RelatedTransactionId], SYSUTCDATETIME() " +
                "FROM [PaymentApprovals];");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserIdNew",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserIdNew",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedTransactionIdNew",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: true);

            // Best-effort default: the earliest Admin user. Operators must reconcile
            // real history using LegacyPaymentApprovalIds.
            migrationBuilder.Sql(
                "UPDATE pa SET [RequestedByUserIdNew] = COALESCE(" +
                "(SELECT TOP (1) u.[Id] FROM [Users] u WHERE u.[Role] = 1 ORDER BY u.[CreatedDate]), " +
                "'00000000-0000-0000-0000-000000000000') " +
                "FROM [PaymentApprovals] pa;");

            migrationBuilder.Sql(
                "UPDATE pa SET [ReviewedByUserIdNew] = " +
                "(SELECT TOP (1) u.[Id] FROM [Users] u WHERE u.[Role] = 1 ORDER BY u.[CreatedDate]) " +
                "FROM [PaymentApprovals] pa " +
                "WHERE pa.[ReviewedByUserId] IS NOT NULL;");

            // Old int transaction ids cannot be mapped to Guid ids.
            migrationBuilder.Sql("UPDATE [PaymentApprovals] SET [RelatedTransactionIdNew] = NULL;");

            migrationBuilder.DropColumn(name: "RequestedByUserId", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "ReviewedByUserId", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "RelatedTransactionId", table: "PaymentApprovals");

            migrationBuilder.RenameColumn(name: "RequestedByUserIdNew", newName: "RequestedByUserId", table: "PaymentApprovals");
            migrationBuilder.RenameColumn(name: "ReviewedByUserIdNew", newName: "ReviewedByUserId", table: "PaymentApprovals");
            migrationBuilder.RenameColumn(name: "RelatedTransactionIdNew", newName: "RelatedTransactionId", table: "PaymentApprovals");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByUserId",
                table: "PaymentApprovals",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PaymentApprovals",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_IdempotencyRecords", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PaymentApprovals_Status_CreatedAt",
                table: "PaymentApprovals",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedDate",
                table: "AuditLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Endpoint_Key",
                table: "IdempotencyRecords",
                columns: new[] { "Endpoint", "Key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IdempotencyRecords");
            migrationBuilder.DropIndex(name: "IX_PaymentApprovals_Status_CreatedAt", table: "PaymentApprovals");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_CreatedDate", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "RowVersion", table: "PaymentApprovals");

            migrationBuilder.AddColumn<int>(
                name: "RequestedByUserIdOld",
                table: "PaymentApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserIdOld",
                table: "PaymentApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedTransactionIdOld",
                table: "PaymentApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE pa SET " +
                "[RequestedByUserIdOld] = COALESCE(l.[OldRequestedByUserId], 0), " +
                "[ReviewedByUserIdOld] = l.[OldReviewedByUserId], " +
                "[RelatedTransactionIdOld] = l.[OldRelatedTransactionId] " +
                "FROM [PaymentApprovals] pa " +
                "LEFT JOIN [LegacyPaymentApprovalIds] l ON l.[ApprovalId] = pa.[Id];");

            migrationBuilder.DropColumn(name: "RequestedByUserId", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "ReviewedByUserId", table: "PaymentApprovals");
            migrationBuilder.DropColumn(name: "RelatedTransactionId", table: "PaymentApprovals");

            migrationBuilder.RenameColumn(name: "RequestedByUserIdOld", newName: "RequestedByUserId", table: "PaymentApprovals");
            migrationBuilder.RenameColumn(name: "ReviewedByUserIdOld", newName: "ReviewedByUserId", table: "PaymentApprovals");
            migrationBuilder.RenameColumn(name: "RelatedTransactionIdOld", newName: "RelatedTransactionId", table: "PaymentApprovals");

            migrationBuilder.AlterColumn<int>(
                name: "RequestedByUserId",
                table: "PaymentApprovals",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.DropTable(name: "LegacyPaymentApprovalIds");
        }
    }
}
