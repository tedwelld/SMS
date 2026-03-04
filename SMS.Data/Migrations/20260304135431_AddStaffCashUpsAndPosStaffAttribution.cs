using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffCashUpsAndPosStaffAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessedByName",
                table: "PosPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StaffUserId",
                table: "PosPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffCashUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffUserId = table.Column<int>(type: "int", nullable: false),
                    StaffName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    CashTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CardTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EcoCashTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCashUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffCashUps_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosPayments_StaffUserId_Timestamp",
                table: "PosPayments",
                columns: new[] { "StaffUserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffCashUps_BusinessDate",
                table: "StaffCashUps",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCashUps_StaffUserId_BusinessDate",
                table: "StaffCashUps",
                columns: new[] { "StaffUserId", "BusinessDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PosPayments_StaffUsers_StaffUserId",
                table: "PosPayments",
                column: "StaffUserId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosPayments_StaffUsers_StaffUserId",
                table: "PosPayments");

            migrationBuilder.DropTable(
                name: "StaffCashUps");

            migrationBuilder.DropIndex(
                name: "IX_PosPayments_StaffUserId_Timestamp",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "ProcessedByName",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "StaffUserId",
                table: "PosPayments");
        }
    }
}
