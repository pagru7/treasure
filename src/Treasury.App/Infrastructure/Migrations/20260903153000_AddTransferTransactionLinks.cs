using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Treasury.App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferTransactionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InflowTransactionId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "OutflowTransactionId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InflowTransactionId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "OutflowTransactionId",
                table: "Transfers");
        }
    }
}
