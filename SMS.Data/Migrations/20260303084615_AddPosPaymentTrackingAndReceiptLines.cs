using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosPaymentTrackingAndReceiptLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "PosPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "PosPayments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "PosPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTransactionId",
                table: "PosPayments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                table: "PosPayments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "PosPayments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "PosPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                table: "PosPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE PosPayments SET ExternalTransactionId = CONCAT('legacy-', Id) WHERE ExternalTransactionId = ''");

            migrationBuilder.CreateTable(
                name: "PosPaymentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PosPaymentId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosPaymentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosPaymentLines_PosPayments_PosPaymentId",
                        column: x => x.PosPaymentId,
                        principalTable: "PosPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosPayments_ExternalTransactionId",
                table: "PosPayments",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PosPayments_Timestamp",
                table: "PosPayments",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PosPaymentLines_PosPaymentId",
                table: "PosPaymentLines",
                column: "PosPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosPaymentLines");

            migrationBuilder.DropIndex(
                name: "IX_PosPayments_ExternalTransactionId",
                table: "PosPayments");

            migrationBuilder.DropIndex(
                name: "IX_PosPayments_Timestamp",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "ExternalTransactionId",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "PointsEarned",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "PosPayments");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "PosPayments");
        }
    }
}
