using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrencyPaymentsAndCashUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffCashUps_StaffUserId_BusinessDate",
                table: "StaffCashUps");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "StaffCashUps",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PosPayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToUsd",
                table: "PosPayments",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateIndex(
                name: "IX_StaffCashUps_StaffUserId_BusinessDate_CurrencyCode",
                table: "StaffCashUps",
                columns: new[] { "StaffUserId", "BusinessDate", "CurrencyCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffCashUps_StaffUserId_BusinessDate_CurrencyCode",
                table: "StaffCashUps");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "StaffCashUps");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToUsd",
                table: "PosPayments");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCashUps_StaffUserId_BusinessDate",
                table: "StaffCashUps",
                columns: new[] { "StaffUserId", "BusinessDate" },
                unique: true);
        }
    }
}
