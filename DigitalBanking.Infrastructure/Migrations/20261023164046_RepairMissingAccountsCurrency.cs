using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingAccountsCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing SQL Server databases can have the old empty
            // AddCurrencyColumnToAccounts migration in __EFMigrationsHistory even
            // though Accounts.Currency was never created. Add it conditionally,
            // without touching any row balance. Legacy balances are explicitly
            // assumed to be TRY because the old schema had no currency value.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'Accounts', N'Currency') IS NULL
                BEGIN
                    ALTER TABLE [Accounts]
                        ADD [Currency] nvarchar(3) NOT NULL
                        CONSTRAINT [DF_Accounts_Currency_LegacyRepair] DEFAULT N'TRY';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: dropping Currency would destroy operator data.
            // A rollback requires a reviewed, backed-up data migration.
        }
    }
}
