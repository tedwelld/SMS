using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;
using SMS.Data.Enums;

namespace SMS.Core.Services;

public class SmsRetailService(SmsDbContext db) : ISmsRetailService
{
    private const decimal PointValue = 0.05m;

    public async Task<BootstrapPayloadDto> GetBootstrapAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
        var customers = await db.Customers
            .AsNoTracking()
            .Include(x => x.PurchaseHistory)
            .ToListAsync(cancellationToken);
        var vendors = await db.Vendors
            .AsNoTracking()
            .Include(x => x.Departments)
            .ToListAsync(cancellationToken);

        return new BootstrapPayloadDto
        {
            Settings = new SmsAppSettingsDto
            {
                Roles = ["Store Manager", "Cashier", "Stock Clerk"],
                ActiveRole = settings.ActiveRole,
                OfflineMode = settings.OfflineMode
            },
            Products = products.Select(MapProduct).ToList(),
            Customers = customers.Select(MapCustomer).ToList(),
            Vendors = vendors.Select(MapVendor).ToList(),
            DraftPurchaseOrders = BuildDraftPurchaseOrders(products, vendors),
            SalesTrend = await db.SalesTrendPoints.AsNoTracking().OrderBy(x => x.Hour).Select(x => new SalesTrendPointDto
            {
                Hour = x.Hour,
                Sales = RoundMoney(x.Sales)
            }).ToListAsync(cancellationToken),
            Reports = new ReportsPayloadDto
            {
                Eod = await GetEodReportAsync(cancellationToken),
                Shrinkage = (await GetShrinkageReportAsync(cancellationToken)).ToList()
            },
            AuditLogs = await db.RetailAuditLogs.AsNoTracking().OrderByDescending(x => x.Timestamp).Take(20)
                .Select(x => new RetailAuditLogDto
                {
                    Id = x.Id,
                    Timestamp = x.Timestamp.ToString("O"),
                    UserRole = x.UserRole,
                    Action = x.Action
                }).ToListAsync(cancellationToken)
        };
    }

    public async Task<SmsAppSettingsDto> PatchSettingsAsync(PatchSettingsRequestDto request, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var knownRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Store Manager", "Cashier", "Stock Clerk"
        };

        if (!string.IsNullOrWhiteSpace(request.ActiveRole) && knownRoles.Contains(request.ActiveRole.Trim()))
        {
            settings.ActiveRole = request.ActiveRole.Trim();
        }

        if (request.OfflineMode.HasValue)
        {
            settings.OfflineMode = request.OfflineMode.Value;
            await AddRetailAuditAsync(
                settings.OfflineMode ? "Offline mode enabled." : "Offline mode disabled.",
                settings.ActiveRole,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SmsAppSettingsDto
        {
            Roles = ["Store Manager", "Cashier", "Stock Clerk"],
            ActiveRole = settings.ActiveRole,
            OfflineMode = settings.OfflineMode
        };
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking().AsQueryable();
        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Sku.ToLower().Contains(term));
        }

        return await query.OrderBy(x => x.Name).Select(x => MapProduct(x)).ToListAsync(cancellationToken);
    }

    public async Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdatePhysicalCountAsync(
        string productId,
        int physicalCount,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        product.PhysicalCount = physicalCount;
        await AddRetailAuditAsync($"Updated physical count for {product.Sku}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);

        return (MapProduct(product), BuildDraftPurchaseOrders(products, vendors));
    }

    public async Task<ProductDto> UpdatePromotionAsync(string productId, UpdatePromotionRequestDto request, string userRole, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        product.PromoType = request.Type.Trim().ToLowerInvariant() switch
        {
            "discount" => PromotionType.Discount,
            "bogo" => PromotionType.Bogo,
            _ => PromotionType.None
        };
        product.PromoValue = product.PromoType == PromotionType.Discount
            ? Math.Clamp(request.Value, 0, 90)
            : 0;

        await AddRetailAuditAsync($"Updated promotion for {product.Sku}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task<IReadOnlyList<CustomerProfileDto>> GetCustomersAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking().Include(x => x.PurchaseHistory).AsQueryable();
        var term = NormalizePhone(search ?? string.Empty);
        var nameTerm = search?.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.ToLower().Contains(nameTerm ?? string.Empty)
                || x.PhoneNumber.Contains(term));
        }

        return await query.OrderByDescending(x => x.Id).Select(x => MapCustomer(x)).ToListAsync(cancellationToken);
    }

    public async Task<CustomerProfileDto?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePhone(phone);
        var customer = await db.Customers.AsNoTracking()
            .Include(x => x.PurchaseHistory)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalized, cancellationToken);

        return customer is null ? null : MapCustomer(customer);
    }

    public async Task<CustomerProfileDto> AddCustomerAsync(CreateRetailCustomerRequestDto request, string userRole, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var phone = NormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(name) || phone.Length < 10)
        {
            throw new InvalidOperationException("Enter a valid name and phone number.");
        }

        var duplicate = await db.Customers.AnyAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("This phone number already belongs to a member.");
        }

        var customer = new Customer
        {
            Name = name,
            PhoneNumber = phone,
            Email = $"{phone}@sms.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            IsActive = true,
            NotificationsEnabled = true,
            PreferredLanguage = "en",
            DailyTransferLimit = 2000m,
            LoyaltyPoints = 0,
            DateCreated = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await AddRetailAuditAsync($"Created loyalty member profile for {name}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapCustomer(customer);
    }

    public async Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken cancellationToken = default)
    {
        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        return vendors.Select(MapVendor).ToList();
    }

    public async Task<IReadOnlyList<DraftPurchaseOrderDto>> GetDraftPurchaseOrdersAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        return BuildDraftPurchaseOrders(products, vendors);
    }

    public async Task<IReadOnlyList<DraftPurchaseOrderDto>> RegenerateDraftPurchaseOrdersAsync(string userRole, CancellationToken cancellationToken = default)
    {
        await AddRetailAuditAsync("Regenerated draft purchase orders.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetDraftPurchaseOrdersAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetailAuditLogDto>> GetAuditAsync(int limit, CancellationToken cancellationToken = default)
    {
        var take = Math.Max(1, limit);
        return await db.RetailAuditLogs.AsNoTracking().OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new RetailAuditLogDto
            {
                Id = x.Id,
                Timestamp = x.Timestamp.ToString("O"),
                UserRole = x.UserRole,
                Action = x.Action
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<EodReportDto> GetEodReportAsync(CancellationToken cancellationToken = default)
    {
        var payments = await db.PosPayments.AsNoTracking().ToListAsync(cancellationToken);
        var cash = payments.Where(x => x.Method == PosPaymentMethod.Cash).Sum(x => x.Amount);
        var card = payments.Where(x => x.Method == PosPaymentMethod.Card).Sum(x => x.Amount);
        var digital = payments.Where(x => x.Method == PosPaymentMethod.Digital).Sum(x => x.Amount);

        return new EodReportDto
        {
            Cash = RoundMoney(cash),
            Card = RoundMoney(card),
            Digital = RoundMoney(digital),
            Total = RoundMoney(cash + card + digital),
            Transactions = payments.Count
        };
    }

    public async Task<IReadOnlyList<ShrinkageReportRowDto>> GetShrinkageReportAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products.AsNoTracking().Select(x => new ShrinkageReportRowDto
        {
            Product = x.Name,
            Sku = x.Sku,
            SystemStock = x.Stock,
            PhysicalCount = x.PhysicalCount,
            Variance = x.PhysicalCount - x.Stock
        }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(CancellationToken cancellationToken = default)
    {
        return await db.SalesTrendPoints.AsNoTracking().OrderBy(x => x.Hour).Select(x => new SalesTrendPointDto
        {
            Hour = x.Hour,
            Sales = RoundMoney(x.Sales)
        }).ToListAsync(cancellationToken);
    }

    public async Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var method = ParsePaymentMethod(request.PaymentMethod);
        if (request.Cart.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        var productIds = request.Cart.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToListAsync(cancellationToken);

        var lineCalculations = new List<(Product Product, int Quantity, decimal Discount, decimal TaxableSubtotal, decimal Tax)>();
        foreach (var line in request.Cart)
        {
            var product = products.FirstOrDefault(x => x.Id == line.ProductId);
            if (product is null)
            {
                continue;
            }

            var qty = Math.Max(0, Math.Min(line.Quantity, product.Stock));
            if (qty <= 0)
            {
                continue;
            }

            var totals = ComputeLineTotals(product, qty);
            lineCalculations.Add((product, qty, totals.Discount, totals.TaxableSubtotal, totals.Tax));
        }

        if (lineCalculations.Count == 0)
        {
            throw new InvalidOperationException("No valid line items available for checkout.");
        }

        var subtotal = lineCalculations.Sum(x => x.TaxableSubtotal);
        var tax = lineCalculations.Sum(x => x.Tax);
        var discount = lineCalculations.Sum(x => x.Discount);
        var gross = subtotal + tax;

        var customerPhone = NormalizePhone(request.CustomerPhone);
        var customer = string.IsNullOrWhiteSpace(customerPhone)
            ? null
            : await db.Customers.Include(x => x.PurchaseHistory)
                .FirstOrDefaultAsync(x => x.PhoneNumber == customerPhone, cancellationToken);

        var requestedPoints = Math.Max(0, request.PointsToRedeem);
        var availablePoints = customer?.LoyaltyPoints ?? 0;
        var allowedPoints = Math.Min(requestedPoints, availablePoints);
        var maxByTotal = (int)Math.Floor(gross / PointValue);
        var pointsRedeemed = Math.Min(allowedPoints, maxByTotal);

        var total = Math.Max(0, gross - pointsRedeemed * PointValue);
        var pointsEarned = (int)Math.Floor(total / 12m);

        foreach (var line in lineCalculations)
        {
            line.Product.Stock = Math.Max(0, line.Product.Stock - line.Quantity);
        }

        var transactionId = BuildTransactionId("tx");
        var now = DateTime.UtcNow;

        db.PosPayments.Add(new PosPayment
        {
            Method = method,
            Amount = RoundMoney(total),
            Timestamp = now
        });

        if (customer is not null)
        {
            customer.LoyaltyPoints = customer.LoyaltyPoints - pointsRedeemed + pointsEarned;
            customer.PurchaseHistory.Add(new CustomerPurchaseRecord
            {
                ExternalTransactionId = transactionId,
                Date = now,
                Total = RoundMoney(total),
                PointsEarned = pointsEarned
            });
        }

        var currentHour = $"{now:HH}:00";
        var slot = await db.SalesTrendPoints.FirstOrDefaultAsync(x => x.Hour == currentHour, cancellationToken);
        if (slot is null)
        {
            db.SalesTrendPoints.Add(new SalesTrendPoint
            {
                Hour = currentHour,
                Sales = RoundMoney(total)
            });
        }
        else
        {
            slot.Sales = RoundMoney(slot.Sales + total);
        }

        var role = string.IsNullOrWhiteSpace(request.UserRole) ? (await GetOrCreateSettingsAsync(cancellationToken)).ActiveRole : request.UserRole.Trim();
        await AddRetailAuditAsync(
            $"Checkout {transactionId} completed ({request.PaymentMethod}) for ${RoundMoney(total):0.00}.",
            role,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new CheckoutResponseDto
        {
            Success = true,
            TransactionId = transactionId,
            Message = $"Checkout complete. Transaction ID: {transactionId}.",
            Totals = new CartTotalsDto
            {
                Subtotal = RoundMoney(subtotal),
                Tax = RoundMoney(tax),
                Discount = RoundMoney(discount),
                PointsRedeemed = pointsRedeemed,
                Total = RoundMoney(total),
                PointsEarned = pointsEarned
            },
            Bootstrap = await GetBootstrapAsync(cancellationToken)
        };
    }

    private async Task<RetailSetting> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.RetailSettings
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new RetailSetting
            {
                ActiveRole = "Store Manager",
                OfflineMode = false
            };
            db.RetailSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    private static (decimal Discount, decimal TaxableSubtotal, decimal Tax) ComputeLineTotals(Product product, int quantity)
    {
        var baseSubtotal = product.Price * quantity;
        decimal discount = 0;

        if (product.PromoType == PromotionType.Discount)
        {
            discount = baseSubtotal * (Math.Clamp(product.PromoValue, 0, 90) / 100m);
        }

        if (product.PromoType == PromotionType.Bogo)
        {
            discount = Math.Floor(quantity / 2m) * product.Price;
        }

        var taxableSubtotal = Math.Max(0, baseSubtotal - discount);
        var tax = taxableSubtotal * product.TaxRate;

        return (discount, taxableSubtotal, tax);
    }

    private static PosPaymentMethod ParsePaymentMethod(string paymentMethod)
    {
        return paymentMethod.Trim().ToLowerInvariant() switch
        {
            "cash" => PosPaymentMethod.Cash,
            "card" => PosPaymentMethod.Card,
            "digital" => PosPaymentMethod.Digital,
            _ => throw new InvalidOperationException("Invalid payment method.")
        };
    }

    private async Task AddRetailAuditAsync(string action, string userRole, CancellationToken cancellationToken)
    {
        db.RetailAuditLogs.Add(new RetailAuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserRole = string.IsNullOrWhiteSpace(userRole) ? "Store Manager" : userRole.Trim(),
            Action = action
        });

        await Task.CompletedTask;
    }

    private static string NormalizePhone(string phone) => string.Concat(phone.Where(char.IsDigit));

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string BuildTransactionId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..22];

    private static ProductDto MapProduct(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        Department = product.Department,
        Price = RoundMoney(product.Price),
        Stock = product.Stock,
        MinStock = product.MinStock,
        TaxRate = product.TaxRate,
        Staple = product.Staple,
        ExpiryDate = product.ExpiryDate?.ToString("yyyy-MM-dd"),
        Promo = new PromotionDto
        {
            Type = product.PromoType switch
            {
                PromotionType.Discount => "discount",
                PromotionType.Bogo => "bogo",
                _ => "none"
            },
            Value = product.PromoValue
        },
        PhysicalCount = product.PhysicalCount
    };

    private static CustomerProfileDto MapCustomer(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Phone = customer.PhoneNumber,
        Points = customer.LoyaltyPoints,
        PurchaseHistory = customer.PurchaseHistory
            .OrderByDescending(x => x.Date)
            .Select(x => new PurchaseRecordDto
            {
                Id = x.ExternalTransactionId,
                Date = x.Date.ToString("O"),
                Total = RoundMoney(x.Total),
                PointsEarned = x.PointsEarned
            }).ToList()
    };

    private static VendorDto MapVendor(Vendor vendor) => new()
    {
        Id = vendor.Id,
        Name = vendor.Name,
        Departments = vendor.Departments.Select(x => x.Department).OrderBy(x => x).ToList(),
        LeadTimeDays = vendor.LeadTimeDays,
        Contact = vendor.Contact,
        Email = vendor.Email
    };

    private static List<DraftPurchaseOrderDto> BuildDraftPurchaseOrders(IReadOnlyList<Product> products, IReadOnlyList<Vendor> vendors)
    {
        var grouped = new Dictionary<int, DraftPurchaseOrderDto>();
        foreach (var product in products)
        {
            if (!(product.Staple && product.Stock <= product.MinStock))
            {
                continue;
            }

            var vendor = vendors.FirstOrDefault(v => v.Departments.Any(d => d.Department == product.Department));
            if (vendor is null)
            {
                continue;
            }

            if (!grouped.TryGetValue(vendor.Id, out var po))
            {
                po = new DraftPurchaseOrderDto
                {
                    Id = BuildTransactionId("po"),
                    VendorId = vendor.Id,
                    VendorName = vendor.Name,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                };
                grouped[vendor.Id] = po;
            }

            po.Lines.Add(new PurchaseOrderLineDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                CurrentStock = product.Stock,
                SuggestedOrderQty = Math.Max(product.MinStock * 2 - product.Stock, product.MinStock)
            });
        }

        return grouped.Values.ToList();
    }
}
