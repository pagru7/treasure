using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.App.Migrations
{
    /// <inheritdoc />
    public partial class another : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetCategories_Categories_CategoryId",
                table: "BudgetCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_PreciousMetalValueEntries_PreciousMetalAssetId",
                table: "PreciousMetalValueEntries");

            migrationBuilder.DropIndex(
                name: "IX_FeedbackItems_HouseholdId",
                table: "FeedbackItems");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetCategories_CategoryId",
                table: "BudgetCategories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetCategories_HouseholdId_CategoryId",
                table: "BudgetCategories");

            migrationBuilder.CreateIndex(
                name: "IX_PreciousMetalValueEntries_PreciousMetalAssetId_ValueDate",
                table: "PreciousMetalValueEntries",
                columns: new[] { "PreciousMetalAssetId", "ValueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreciousMetalValueEntries_PreciousMetalAssetId_ValueDate",
                table: "PreciousMetalValueEntries");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PreciousMetalValueEntries_PreciousMetalAssetId",
                table: "PreciousMetalValueEntries",
                column: "PreciousMetalAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackItems_HouseholdId",
                table: "FeedbackItems",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_CategoryId",
                table: "BudgetCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_HouseholdId_CategoryId",
                table: "BudgetCategories",
                columns: new[] { "HouseholdId", "CategoryId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetCategories_Categories_CategoryId",
                table: "BudgetCategories",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
