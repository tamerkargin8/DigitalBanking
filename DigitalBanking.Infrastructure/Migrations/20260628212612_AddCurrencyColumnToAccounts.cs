using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalBanking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyColumnToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The original baseline/initial DDL never created Accounts.Currency.
            // This migration was scaffolded as an empty marker, leaving fresh SQL
            // Server schemas without the column. Add it here for the fresh chain;
            // existing databases that already recorded this migration use the
            // additive RepairMissingAccountsCurrency migration.
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Accounts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: dropping Currency on rollback would destroy
            // operator data. Use a reviewed data-migration if a rollback is needed.
        }
    }
}
