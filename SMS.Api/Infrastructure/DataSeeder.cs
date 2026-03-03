using Microsoft.EntityFrameworkCore;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;
using SMS.Data.Enums;

namespace SMS.Api.Infrastructure;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        await SeedStaffUsersAsync(db, cancellationToken);
        await SeedRetailDataAsync(db, cancellationToken);
        await SeedWalletDemoAsync(db, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStaffUsersAsync(SmsDbContext db, CancellationToken cancellationToken)
    {
        await EnsureStaffUserAsync(db, new StaffUser
        {
            Username = "admin",
            Name = "System Administrator",
            Email = "admin@sms.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = "Admin",
            PhoneNumber = "+263700000001",
            Department = "Platform",
            NotificationsEnabled = true,
            CanApproveReversals = true,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        }, cancellationToken);

        await EnsureStaffUserAsync(db, new StaffUser
        {
            Username = "admin_ops",
            Name = "Operations Administrator",
            Email = "admin.ops@sms.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OpsAdmin@123!"),
            Role = "Admin",
            PhoneNumber = "+263700000002",
            Department = "Operations",
            NotificationsEnabled = true,
            CanApproveReversals = true,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        }, cancellationToken);

        await EnsureStaffUserAsync(db, new StaffUser
        {
            Username = "staff_pos",
            Name = "POS Staff User",
            Email = "staff@sms.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123"),
            Role = "Staff",
            PhoneNumber = "+263700000010",
            Department = "POS",
            NotificationsEnabled = true,
            CanApproveReversals = false,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        }, cancellationToken);
    }

    private static async Task SeedRetailDataAsync(SmsDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.RetailSettings
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            db.RetailSettings.Add(new RetailSetting
            {
                ActiveRole = "Stock Clerk",
                OfflineMode = false
            });
        }

        if (!await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                new Product
                {
                    Id = "p1",
                    Name = "Whole Wheat Bread",
                    Sku = "GRC-1001",
                    Department = "Grocery",
                    Price = 2.99m,
                    Stock = 48,
                    MinStock = 25,
                    TaxRate = 0.03m,
                    Staple = true,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
                    PromoType = PromotionType.Bogo,
                    PromoValue = 0,
                    PhysicalCount = 48
                },
                new Product
                {
                    Id = "p2",
                    Name = "Organic Milk 1L",
                    Sku = "DRY-2001",
                    Department = "Dairy",
                    Price = 3.75m,
                    Stock = 19,
                    MinStock = 22,
                    TaxRate = 0.03m,
                    Staple = true,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5)),
                    PromoType = PromotionType.Discount,
                    PromoValue = 10,
                    PhysicalCount = 18
                },
                new Product
                {
                    Id = "p3",
                    Name = "Eggs (12 pack)",
                    Sku = "DRY-2009",
                    Department = "Dairy",
                    Price = 4.50m,
                    Stock = 32,
                    MinStock = 18,
                    TaxRate = 0.03m,
                    Staple = true,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(12)),
                    PromoType = PromotionType.None,
                    PromoValue = 0,
                    PhysicalCount = 31
                },
                new Product
                {
                    Id = "p4",
                    Name = "USB-C Charger",
                    Sku = "ELC-4003",
                    Department = "Electronics",
                    Price = 24.99m,
                    Stock = 9,
                    MinStock = 10,
                    TaxRate = 0.08m,
                    Staple = false,
                    PromoType = PromotionType.Discount,
                    PromoValue = 12,
                    PhysicalCount = 8
                },
                new Product
                {
                    Id = "p5",
                    Name = "Dish Soap 500ml",
                    Sku = "HSD-5007",
                    Department = "Household",
                    Price = 6.20m,
                    Stock = 15,
                    MinStock = 20,
                    TaxRate = 0.07m,
                    Staple = true,
                    PromoType = PromotionType.None,
                    PromoValue = 0,
                    PhysicalCount = 13
                },
                new Product
                {
                    Id = "p6",
                    Name = "Bananas (1kg)",
                    Sku = "PRD-7001",
                    Department = "Produce",
                    Price = 1.99m,
                    Stock = 61,
                    MinStock = 35,
                    TaxRate = 0,
                    Staple = true,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
                    PromoType = PromotionType.None,
                    PromoValue = 0,
                    PhysicalCount = 59
                },
                new Product
                {
                    Id = "p7",
                    Name = "Rice 5kg",
                    Sku = "GRC-1018",
                    Department = "Grocery",
                    Price = 12.95m,
                    Stock = 14,
                    MinStock = 16,
                    TaxRate = 0.03m,
                    Staple = true,
                    PromoType = PromotionType.Discount,
                    PromoValue = 5,
                    PhysicalCount = 14
                },
                new Product
                {
                    Id = "p8",
                    Name = "Bluetooth Earbuds",
                    Sku = "ELC-4022",
                    Department = "Electronics",
                    Price = 59m,
                    Stock = 6,
                    MinStock = 8,
                    TaxRate = 0.08m,
                    Staple = false,
                    PromoType = PromotionType.None,
                    PromoValue = 0,
                    PhysicalCount = 6
                },
                new Product
                {
                    Id = "p9",
                    Name = "Paper Towels (6 roll)",
                    Sku = "HSD-5012",
                    Department = "Household",
                    Price = 9.40m,
                    Stock = 25,
                    MinStock = 18,
                    TaxRate = 0.07m,
                    Staple = true,
                    PromoType = PromotionType.Bogo,
                    PromoValue = 0,
                    PhysicalCount = 23
                }
            );
        }

        if (!await db.Vendors.AnyAsync(cancellationToken))
        {
            db.Vendors.AddRange(
                new Vendor
                {
                    Name = "FreshFlow Foods",
                    LeadTimeDays = 2,
                    Contact = "Mia Warren",
                    Email = "mia@freshflow.example",
                    Departments =
                    [
                        new VendorDepartment { Department = "Grocery" },
                        new VendorDepartment { Department = "Produce" },
                        new VendorDepartment { Department = "Dairy" }
                    ]
                },
                new Vendor
                {
                    Name = "HomeNex Supplies",
                    LeadTimeDays = 3,
                    Contact = "Noah Mills",
                    Email = "noah@homenex.example",
                    Departments =
                    [
                        new VendorDepartment { Department = "Household" }
                    ]
                },
                new Vendor
                {
                    Name = "CircuitHub Distribution",
                    LeadTimeDays = 5,
                    Contact = "Lena Price",
                    Email = "lena@circuithub.example",
                    Departments =
                    [
                        new VendorDepartment { Department = "Electronics" }
                    ]
                }
            );
        }

        if (!await db.SalesTrendPoints.AnyAsync(cancellationToken))
        {
            db.SalesTrendPoints.AddRange(
                new SalesTrendPoint { Hour = "09:00", Sales = 184.20m },
                new SalesTrendPoint { Hour = "10:00", Sales = 421.88m },
                new SalesTrendPoint { Hour = "11:00", Sales = 233.45m }
            );
        }

        if (!await db.PosPayments.AnyAsync(cancellationToken))
        {
            db.PosPayments.AddRange(
                new PosPayment
                {
                    ExternalTransactionId = "tx-seed-0001",
                    Method = PosPaymentMethod.Cash,
                    Subtotal = 178.84m,
                    Tax = 5.36m,
                    Discount = 0m,
                    PointsRedeemed = 0,
                    PointsEarned = 15,
                    Amount = 184.20m,
                    Timestamp = DateTime.UtcNow.AddHours(-4)
                },
                new PosPayment
                {
                    ExternalTransactionId = "tx-seed-0002",
                    Method = PosPaymentMethod.Card,
                    Subtotal = 405.65m,
                    Tax = 16.23m,
                    Discount = 0m,
                    PointsRedeemed = 0,
                    PointsEarned = 35,
                    Amount = 421.88m,
                    Timestamp = DateTime.UtcNow.AddHours(-3)
                },
                new PosPayment
                {
                    ExternalTransactionId = "tx-seed-0003",
                    Method = PosPaymentMethod.Digital,
                    Subtotal = 226.65m,
                    Tax = 6.80m,
                    Discount = 0m,
                    PointsRedeemed = 0,
                    PointsEarned = 19,
                    Amount = 233.45m,
                    Timestamp = DateTime.UtcNow.AddHours(-2)
                }
            );
        }

        if (!await db.RetailAuditLogs.AnyAsync(cancellationToken))
        {
            db.RetailAuditLogs.Add(new RetailAuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserRole = "Stock Clerk",
                Action = "Initial SMS retail dataset loaded."
            });
        }

        if (!await db.Customers.AnyAsync(cancellationToken))
        {
            var first = new Customer
            {
                Name = "Jordan Carter",
                PhoneNumber = "5550132001",
                Email = "jordan.carter@sms.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                IsActive = true,
                NotificationsEnabled = true,
                PreferredLanguage = "en",
                DailyTransferLimit = 2000m,
                LoyaltyPoints = 140,
                DateCreated = DateTime.UtcNow.AddDays(-20)
            };
            first.PurchaseHistory.Add(new CustomerPurchaseRecord
            {
                ExternalTransactionId = "tx-1001",
                Date = DateTime.UtcNow.AddDays(-12),
                Total = 58.49m,
                PointsEarned = 4
            });
            first.PurchaseHistory.Add(new CustomerPurchaseRecord
            {
                ExternalTransactionId = "tx-1007",
                Date = DateTime.UtcNow.AddDays(-11),
                Total = 22.15m,
                PointsEarned = 1
            });

            var second = new Customer
            {
                Name = "Avery Nguyen",
                PhoneNumber = "5550132017",
                Email = "avery.nguyen@sms.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                IsActive = true,
                NotificationsEnabled = true,
                PreferredLanguage = "en",
                DailyTransferLimit = 2000m,
                LoyaltyPoints = 62,
                DateCreated = DateTime.UtcNow.AddDays(-18)
            };
            second.PurchaseHistory.Add(new CustomerPurchaseRecord
            {
                ExternalTransactionId = "tx-1011",
                Date = DateTime.UtcNow.AddDays(-10),
                Total = 78.20m,
                PointsEarned = 6
            });

            db.Customers.AddRange(
                first,
                second,
                new Customer
                {
                    Name = "Morgan Diaz",
                    PhoneNumber = "5550132098",
                    Email = "morgan.diaz@sms.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                    IsActive = true,
                    NotificationsEnabled = true,
                    PreferredLanguage = "en",
                    DailyTransferLimit = 2000m,
                    LoyaltyPoints = 21,
                    DateCreated = DateTime.UtcNow.AddDays(-15)
                }
            );
        }
    }

    private static async Task SeedWalletDemoAsync(SmsDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Wallets.AnyAsync(cancellationToken))
        {
            return;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return;
        }

        var account = new CustomerAccount
        {
            CustomerId = customer.Id,
            AccountNumber = $"ACC-{customer.Id:D6}-1001",
            Balance = 250m,
            IsFrozen = false
        };

        db.CustomerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        var wallet = new Wallet
        {
            CustomerAccountId = account.Id,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);

        db.NfcCards.Add(new NfcCard
        {
            WalletId = wallet.Id,
            CardUid = $"NFC-{customer.PhoneNumber[^6..]}-A1",
            PhoneNumber = customer.PhoneNumber
        });

        db.QrTokens.Add(new QrToken
        {
            WalletId = wallet.Id,
            Token = Guid.NewGuid().ToString("N"),
            Expiry = DateTime.UtcNow.AddDays(30),
            MaxUsage = 500,
            CurrentUsage = 0,
            Pin = "1234"
        });

        db.AccessMethods.Add(new AccessMethod
        {
            WalletId = wallet.Id,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        });

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = account.Id,
            TransactionType = "Deposit",
            Channel = "Cash",
            Amount = 250m,
            BalanceAfter = 250m,
            Reference = "DEP-SEED-001",
            Notes = "Seed opening balance",
            CreatedAt = DateTime.UtcNow
        });
    }

    private static async Task EnsureStaffUserAsync(SmsDbContext db, StaffUser seed, CancellationToken cancellationToken)
    {
        var existing = await db.StaffUsers.FirstOrDefaultAsync(
            x => x.Username == seed.Username || x.Email == seed.Email,
            cancellationToken);

        if (existing is null)
        {
            db.StaffUsers.Add(seed);
            return;
        }

        existing.Username = seed.Username;
        existing.Name = seed.Name;
        existing.Email = seed.Email;
        existing.PasswordHash = seed.PasswordHash;
        existing.Role = seed.Role;
        existing.PhoneNumber = seed.PhoneNumber;
        existing.Department = seed.Department;
        existing.NotificationsEnabled = seed.NotificationsEnabled;
        existing.CanApproveReversals = seed.CanApproveReversals;
        existing.IsActive = seed.IsActive;

        if (existing.DateCreated == default)
        {
            existing.DateCreated = seed.DateCreated;
        }
    }
}
