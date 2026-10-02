using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.App.Migrations
{
    /// <inheritdoc />
    public partial class transaction_type_enum_int : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Transactions"
                ALTER COLUMN "Type" TYPE integer
                USING CASE lower(trim("Type"))
                    WHEN 'expense' THEN 0
                    WHEN 'income' THEN 1
                    WHEN 'transfer' THEN 2
                    WHEN 'transfer-in' THEN 3
                    WHEN 'transfer-out' THEN 4
                    WHEN 'balance-correction' THEN 5
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Transactions"
                ALTER COLUMN "Type" TYPE text
                USING CASE "Type"
                    WHEN 0 THEN 'expense'
                    WHEN 1 THEN 'income'
                    WHEN 2 THEN 'transfer'
                    WHEN 3 THEN 'transfer-in'
                    WHEN 4 THEN 'transfer-out'
                    WHEN 5 THEN 'balance-correction'
                    ELSE 'expense'
                END;
                """);
        }
    }
}
