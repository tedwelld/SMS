using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivitySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "en"),
                    DailyTransferLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 2000m),
                    LoyaltyPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    MinStock = table.Column<int>(type: "int", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(8,4)", nullable: false),
                    Staple = table.Column<bool>(type: "bit", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PromoType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PromoValue = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    PhysicalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetailAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserRole = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActiveRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OfflineMode = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesTrendPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hour = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Sales = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesTrendPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanApproveReversals = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAccounts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPurchaseRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PointsEarned = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPurchaseRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPurchaseRecords_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsNotifications_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SenderCustomerId = table.Column<int>(type: "int", nullable: true),
                    SenderStaffUserId = table.Column<int>(type: "int", nullable: true),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientCustomerId = table.Column<int>(type: "int", nullable: true),
                    RecipientStaffUserId = table.Column<int>(type: "int", nullable: true),
                    IsReadByRecipient = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBySender = table.Column<bool>(type: "bit", nullable: false),
                    DeletedByRecipient = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_Customers_RecipientCustomerId",
                        column: x => x.RecipientCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_Customers_SenderCustomerId",
                        column: x => x.SenderCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_StaffUsers_RecipientStaffUserId",
                        column: x => x.RecipientStaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_StaffUsers_SenderStaffUserId",
                        column: x => x.SenderStaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VendorDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorDepartments_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentFloatTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFloatTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentFloatTransactions_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirtimePurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AirtimeBalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VoucherCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirtimePurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirtimePurchases_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    BillerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillPayments_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MerchantTillPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantTillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantTillPayments_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wallets_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerAccountId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CounterpartyPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_CustomerAccounts_CustomerAccountId",
                        column: x => x.CustomerAccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessMethods_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffUserId = table.Column<int>(type: "int", nullable: false),
                    WalletId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NfcCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    CardUid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfcCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NfcCards_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Expiry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxUsage = table.Column<int>(type: "int", nullable: false),
                    CurrentUsage = table.Column<int>(type: "int", nullable: false),
                    Pin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrTokens", x => x.Id);
                    table.CheckConstraint("CK_QrTokens_Usage", "[CurrentUsage] <= [MaxUsage] AND [CurrentUsage] >= 0");
                    table.ForeignKey(
                        name: "FK_QrTokens_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    WalletTransactionId = table.Column<int>(type: "int", nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountNotifications_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountNotifications_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TransactionReversals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletTransactionId = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffUserId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReviewedByStaffUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReversalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_StaffUsers_RequestedByStaffUserId",
                        column: x => x.RequestedByStaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_StaffUsers_ReviewedByStaffUserId",
                        column: x => x.ReviewedByStaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransactionReversals_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MobileAppSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    QrTokenId = table.Column<int>(type: "int", nullable: true),
                    JwtToken = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Expiry = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileAppSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileAppSessions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobileAppSessions_QrTokens_QrTokenId",
                        column: x => x.QrTokenId,
                        principalTable: "QrTokens",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessMethods_WalletId",
                table: "AccessMethods",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountNotifications_CustomerId_CreatedAt",
                table: "AccountNotifications",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountNotifications_WalletTransactionId",
                table: "AccountNotifications",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySnapshots_CreatedAt",
                table: "ActivitySnapshots",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFloatTransactions_CustomerAccountId_CreatedAt",
                table: "AgentFloatTransactions",
                columns: new[] { "CustomerAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AirtimePurchases_CustomerAccountId_CreatedAt",
                table: "AirtimePurchases",
                columns: new[] { "CustomerAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_StaffUserId",
                table: "AuditLogs",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_WalletId",
                table: "AuditLogs",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_CustomerAccountId_CreatedAt",
                table: "BillPayments",
                columns: new[] { "CustomerAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_RecipientCustomerId_RecipientStaffUserId_CreatedAt",
                table: "CommunicationMessages",
                columns: new[] { "RecipientCustomerId", "RecipientStaffUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_RecipientStaffUserId",
                table: "CommunicationMessages",
                column: "RecipientStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_SenderCustomerId_SenderStaffUserId_CreatedAt",
                table: "CommunicationMessages",
                columns: new[] { "SenderCustomerId", "SenderStaffUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_SenderStaffUserId",
                table: "CommunicationMessages",
                column: "SenderStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccounts_AccountNumber",
                table: "CustomerAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccounts_CustomerId",
                table: "CustomerAccounts",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPurchaseRecords_CustomerId_Date",
                table: "CustomerPurchaseRecords",
                columns: new[] { "CustomerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PhoneNumber",
                table: "Customers",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantTillPayments_CustomerAccountId_CreatedAt",
                table: "MerchantTillPayments",
                columns: new[] { "CustomerAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppSessions_CustomerId",
                table: "MobileAppSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppSessions_QrTokenId",
                table: "MobileAppSessions",
                column: "QrTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_NfcCards_CardUid",
                table: "NfcCards",
                column: "CardUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NfcCards_PhoneNumber",
                table: "NfcCards",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NfcCards_WalletId",
                table: "NfcCards",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrTokens_Token",
                table: "QrTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrTokens_WalletId",
                table: "QrTokens",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTrendPoints_Hour",
                table: "SalesTrendPoints",
                column: "Hour",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsNotifications_CustomerId",
                table: "SmsNotifications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Username",
                table: "StaffUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_RequestedByStaffUserId",
                table: "TransactionReversals",
                column: "RequestedByStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_ReviewedByStaffUserId",
                table: "TransactionReversals",
                column: "ReviewedByStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_Status",
                table: "TransactionReversals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_WalletTransactionId",
                table: "TransactionReversals",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorDepartments_VendorId_Department",
                table: "VendorDepartments",
                columns: new[] { "VendorId", "Department" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerAccountId",
                table: "Wallets",
                column: "CustomerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_CustomerAccountId_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "CustomerAccountId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessMethods");

            migrationBuilder.DropTable(
                name: "AccountNotifications");

            migrationBuilder.DropTable(
                name: "ActivitySnapshots");

            migrationBuilder.DropTable(
                name: "AgentFloatTransactions");

            migrationBuilder.DropTable(
                name: "AirtimePurchases");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropTable(
                name: "CommunicationMessages");

            migrationBuilder.DropTable(
                name: "CustomerPurchaseRecords");

            migrationBuilder.DropTable(
                name: "MerchantTillPayments");

            migrationBuilder.DropTable(
                name: "MobileAppSessions");

            migrationBuilder.DropTable(
                name: "NfcCards");

            migrationBuilder.DropTable(
                name: "PosPayments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "RetailAuditLogs");

            migrationBuilder.DropTable(
                name: "RetailSettings");

            migrationBuilder.DropTable(
                name: "SalesTrendPoints");

            migrationBuilder.DropTable(
                name: "SmsNotifications");

            migrationBuilder.DropTable(
                name: "TransactionReversals");

            migrationBuilder.DropTable(
                name: "VendorDepartments");

            migrationBuilder.DropTable(
                name: "QrTokens");

            migrationBuilder.DropTable(
                name: "StaffUsers");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "CustomerAccounts");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
