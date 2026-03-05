using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;
using SMS.Data.Enums;

namespace SMS.Core.Services;

public class SmsRetailService(SmsDbContext db, IConfiguration configuration) : ISmsRetailService
{
    private const decimal PointValue = 0.05m;
    private const string BaseCurrencyCode = "USD";
    private const string ReceiptVerificationSecretConfigPath = "ReceiptVerification:SecretKey";
    private const string JwtSecretConfigPath = "Jwt:SecretKey";
    private static readonly IReadOnlyDictionary<string, decimal> SupportedCurrencyRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        [BaseCurrencyCode] = 1m,
        ["ZAR"] = 18.50m,
        ["ZWG"] = 13.75m
    };

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
                }).ToListAsync(cancellationToken),
            RecentPayments = (await GetPaymentsAsync(null, null, null, null, null, 20, cancellationToken)).ToList()
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

    public async Task<ProductDto> CreateProductAsync(
        CreateProductRequestDto request,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can add products.");
        }

        var name = request.Name.Trim();
        var sku = request.Sku.Trim().ToUpperInvariant();
        var department = request.Department.Trim();
        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(sku)
            || string.IsNullOrWhiteSpace(department))
        {
            throw new InvalidOperationException("Name, SKU, and department are required.");
        }

        if (request.Price < 0 || request.Stock < 0 || request.MinStock < 0 || request.TaxRate < 0)
        {
            throw new InvalidOperationException("Price, stock, minimum stock, and tax rate must be non-negative.");
        }

        var duplicate = await db.Products.AnyAsync(x => x.Sku == sku, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("A product with this SKU already exists.");
        }

        var arrivalDate = ParseDateOnlyOrDefault(request.ArrivalDate, DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var expiryDate = ParseDateOnlyOrNull(request.ExpiryDate);

        var product = new Product
        {
            Id = BuildTransactionId("prd"),
            Name = name,
            Sku = sku,
            Department = department,
            Price = RoundMoney(request.Price),
            Stock = request.Stock,
            MinStock = request.MinStock,
            TaxRate = Math.Clamp(request.TaxRate, 0, 1),
            Staple = request.Staple,
            ArrivalDate = arrivalDate,
            ExpiryDate = expiryDate,
            PromoType = PromotionType.None,
            PromoValue = 0,
            PhysicalCount = request.Stock
        };

        db.Products.Add(product);
        await AddRetailAuditAsync($"Added product {product.Sku} ({product.Name}).", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task DeleteProductAsync(string productId, string userRole, CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can delete products.");
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        db.Products.Remove(product);
        await AddRetailAuditAsync($"Deleted product {product.Sku} ({product.Name}).", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductDto> UpdateProductAsync(
        string productId,
        UpdateProductRequestDto request,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can edit products.");
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        var name = request.Name.Trim();
        var sku = request.Sku.Trim().ToUpperInvariant();
        var department = request.Department.Trim();
        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(sku)
            || string.IsNullOrWhiteSpace(department))
        {
            throw new InvalidOperationException("Name, SKU, and department are required.");
        }

        if (request.Price < 0 || request.Stock < 0 || request.MinStock < 0 || request.TaxRate < 0)
        {
            throw new InvalidOperationException("Price, stock, minimum stock, and tax rate must be non-negative.");
        }

        var duplicate = await db.Products
            .AnyAsync(x => x.Id != productId && x.Sku == sku, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("A product with this SKU already exists.");
        }

        var oldSku = product.Sku;
        var oldName = product.Name;

        product.Name = name;
        product.Sku = sku;
        product.Department = department;
        product.Price = RoundMoney(request.Price);
        product.Stock = request.Stock;
        product.MinStock = request.MinStock;
        product.TaxRate = Math.Clamp(request.TaxRate, 0, 1);
        product.Staple = request.Staple;
        product.ArrivalDate = ParseDateOnlyOrDefault(request.ArrivalDate, product.ArrivalDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));
        product.ExpiryDate = ParseDateOnlyOrNull(request.ExpiryDate);
        product.PhysicalCount = request.Stock;

        await AddRetailAuditAsync(
            $"Updated product {oldSku} ({oldName}) -> {product.Sku} ({product.Name}).",
            userRole,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdatePhysicalCountAsync(
        string productId,
        int physicalCount,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can update physical counts.");
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        product.PhysicalCount = physicalCount;
        await AddRetailAuditAsync($"Updated physical count for {product.Sku}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);

        return (MapProduct(product), BuildDraftPurchaseOrders(products, vendors));
    }

    public async Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdateStockAsync(
        string productId,
        int quantity,
        string mode,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can update stock.");
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        if (quantity < 0)
        {
            throw new InvalidOperationException("Quantity cannot be negative.");
        }

        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "set" : mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("set" or "add"))
        {
            throw new InvalidOperationException("Mode must be 'set' or 'add'.");
        }

        var oldStock = product.Stock;
        if (normalizedMode == "set")
        {
            product.Stock = quantity;
        }
        else
        {
            if (product.Stock > int.MaxValue - quantity)
            {
                throw new InvalidOperationException("Stock quantity is too large.");
            }

            product.Stock += quantity;
        }

        // Manual stock entry represents physically verified stock being entered by admin.
        product.PhysicalCount = product.Stock;

        await AddRetailAuditAsync(
            $"Updated stock for {product.Sku}: {oldStock} -> {product.Stock} ({normalizedMode}).",
            userRole,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);

        return (MapProduct(product), BuildDraftPurchaseOrders(products, vendors));
    }

    public async Task<ProductDto> UpdatePromotionAsync(string productId, UpdatePromotionRequestDto request, string userRole, CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can update pricing and promotions.");
        }

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

    public async Task<VendorDto> CreateVendorAsync(
        CreateVendorRequestDto request,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can create vendors.");
        }

        var name = request.Name.Trim();
        var contact = request.Contact.Trim();
        var email = request.Email.Trim();
        var leadTimeDays = Math.Max(1, request.LeadTimeDays);
        var departments = NormalizeVendorDepartments(request.Departments);

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(contact)
            || string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Vendor name, contact, and email are required.");
        }

        if (departments.Count == 0)
        {
            throw new InvalidOperationException("At least one vendor department is required.");
        }

        var duplicate = await db.Vendors
            .AnyAsync(x => x.Name.ToLower() == name.ToLower() || x.Email.ToLower() == email.ToLower(), cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("A vendor with this name or email already exists.");
        }

        var vendor = new Vendor
        {
            Name = name,
            Contact = contact,
            Email = email,
            LeadTimeDays = leadTimeDays,
            Departments = departments.Select(department => new VendorDepartment
            {
                Department = department
            }).ToList()
        };

        db.Vendors.Add(vendor);
        await AddRetailAuditAsync($"Added vendor {vendor.Name}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapVendor(vendor);
    }

    public async Task<VendorDto> UpdateVendorAsync(
        int vendorId,
        UpdateVendorRequestDto request,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can update vendors.");
        }

        var vendor = await db.Vendors
            .Include(x => x.Departments)
            .FirstOrDefaultAsync(x => x.Id == vendorId, cancellationToken)
            ?? throw new KeyNotFoundException("Vendor not found.");

        var name = request.Name.Trim();
        var contact = request.Contact.Trim();
        var email = request.Email.Trim();
        var leadTimeDays = Math.Max(1, request.LeadTimeDays);
        var departments = NormalizeVendorDepartments(request.Departments);

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(contact)
            || string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Vendor name, contact, and email are required.");
        }

        if (departments.Count == 0)
        {
            throw new InvalidOperationException("At least one vendor department is required.");
        }

        var duplicate = await db.Vendors
            .AnyAsync(
                x => x.Id != vendorId
                    && (x.Name.ToLower() == name.ToLower() || x.Email.ToLower() == email.ToLower()),
                cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("A vendor with this name or email already exists.");
        }

        vendor.Name = name;
        vendor.Contact = contact;
        vendor.Email = email;
        vendor.LeadTimeDays = leadTimeDays;

        var requestedDepartments = departments.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var departmentsToRemove = vendor.Departments
            .Where(existing => !requestedDepartments.Contains(existing.Department))
            .ToList();
        if (departmentsToRemove.Count > 0)
        {
            db.VendorDepartments.RemoveRange(departmentsToRemove);
        }

        var existingDepartments = vendor.Departments
            .Select(x => x.Department)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var department in departments)
        {
            if (!existingDepartments.Contains(department))
            {
                vendor.Departments.Add(new VendorDepartment
                {
                    Department = department
                });
            }
        }

        await AddRetailAuditAsync($"Updated vendor {vendor.Name}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return MapVendor(vendor);
    }

    public async Task DeleteVendorAsync(int vendorId, string userRole, CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can delete vendors.");
        }

        var vendor = await db.Vendors
            .FirstOrDefaultAsync(x => x.Id == vendorId, cancellationToken)
            ?? throw new KeyNotFoundException("Vendor not found.");

        db.Vendors.Remove(vendor);
        await AddRetailAuditAsync($"Deleted vendor {vendor.Name}.", userRole, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DraftPurchaseOrderDto>> GetDraftPurchaseOrdersAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products.AsNoTracking().ToListAsync(cancellationToken);
        var vendors = await db.Vendors.AsNoTracking().Include(x => x.Departments).ToListAsync(cancellationToken);
        return BuildDraftPurchaseOrders(products, vendors);
    }

    public async Task<IReadOnlyList<DraftPurchaseOrderDto>> RegenerateDraftPurchaseOrdersAsync(string userRole, CancellationToken cancellationToken = default)
    {
        if (!IsAdminRole(userRole))
        {
            throw new UnauthorizedAccessException("Only admins can regenerate draft purchase orders.");
        }

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
        var cash = payments.Where(x => x.Method == PosPaymentMethod.Cash).Sum(ConvertToBaseCurrency);
        var card = payments.Where(x => x.Method == PosPaymentMethod.Card).Sum(ConvertToBaseCurrency);
        var ecoCash = payments.Where(x => x.Method == PosPaymentMethod.EcoCash).Sum(ConvertToBaseCurrency);

        return new EodReportDto
        {
            Cash = RoundMoney(cash),
            Card = RoundMoney(card),
            Digital = RoundMoney(ecoCash),
            Total = RoundMoney(cash + card + ecoCash),
            Transactions = payments.Count,
            CurrencyCode = BaseCurrencyCode
        };
    }

    public async Task<IReadOnlyList<PaymentTrackingRecordDto>> GetPaymentsAsync(
        DateTime? from,
        DateTime? to,
        string? method,
        string? query,
        string? currency,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        var paymentsQuery = db.PosPayments
            .AsNoTracking()
            .Include(x => x.Lines)
            .AsQueryable();

        if (from.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(x => x.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(x => x.Timestamp <= to.Value);
        }

        if (TryParsePaymentMethod(method, out var paymentMethod))
        {
            paymentsQuery = paymentsQuery.Where(x => x.Method == paymentMethod);
        }

        if (TryNormalizeCurrencyCode(currency, out var normalizedCurrency))
        {
            paymentsQuery = paymentsQuery.Where(x => x.CurrencyCode == normalizedCurrency);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var rawQuery = query.Trim();
            var normalizedPhone = NormalizePhone(rawQuery);

            paymentsQuery = paymentsQuery.Where(x =>
                x.ExternalTransactionId.Contains(rawQuery)
                || ((x.CustomerName ?? string.Empty).Contains(rawQuery))
                || (!string.IsNullOrWhiteSpace(normalizedPhone) && (x.CustomerPhone ?? string.Empty).Contains(normalizedPhone)));
        }

        var payments = await paymentsQuery
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .ToListAsync(cancellationToken);

        return payments.Select(MapPaymentTracking).ToList();
    }

    public async Task<StaffCashUpDto> SubmitDailyCashUpAsync(SubmitCashUpRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.StaffUserId <= 0)
        {
            throw new InvalidOperationException("A valid staff user id is required.");
        }

        var targetCurrencyCode = NormalizeCurrencyCode(request.CurrencyCode);
        var targetExchangeRate = ResolveExchangeRateToUsd(targetCurrencyCode);
        var businessDate = ParseBusinessDateOrUtcToday(request.BusinessDate);
        var periodStartUtc = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(1);
        var now = DateTime.UtcNow;

        var staff = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.StaffUserId, cancellationToken);
        if (staff is null)
        {
            throw new InvalidOperationException("Staff user was not found.");
        }

        var staffName = string.IsNullOrWhiteSpace(request.StaffName)
            ? staff.Name
            : request.StaffName.Trim();

        if (string.IsNullOrWhiteSpace(staffName))
        {
            staffName = $"Staff #{request.StaffUserId}";
        }

        var payments = await db.PosPayments
            .AsNoTracking()
            .Where(x =>
                x.StaffUserId == request.StaffUserId
                && x.Timestamp >= periodStartUtc
                && x.Timestamp < periodEndUtc)
            .ToListAsync(cancellationToken);

        decimal ToTargetCurrency(PosPayment payment) =>
            ConvertFromBaseCurrency(ConvertToBaseCurrency(payment), targetExchangeRate);

        var cash = payments.Where(x => x.Method == PosPaymentMethod.Cash).Sum(ToTargetCurrency);
        var card = payments.Where(x => x.Method == PosPaymentMethod.Card).Sum(ToTargetCurrency);
        var ecoCash = payments.Where(x => x.Method == PosPaymentMethod.EcoCash).Sum(ToTargetCurrency);
        var total = cash + card + ecoCash;
        var transactionCount = payments.Count;

        var existing = await db.StaffCashUps.FirstOrDefaultAsync(
            x =>
                x.StaffUserId == request.StaffUserId
                && x.BusinessDate == periodStartUtc.Date
                && x.CurrencyCode == targetCurrencyCode,
            cancellationToken);

        if (existing is null)
        {
            existing = new StaffCashUp
            {
                StaffUserId = request.StaffUserId,
                StaffName = staffName,
                CurrencyCode = targetCurrencyCode,
                BusinessDate = periodStartUtc.Date,
                CashTotal = RoundMoney(cash),
                CardTotal = RoundMoney(card),
                EcoCashTotal = RoundMoney(ecoCash),
                Total = RoundMoney(total),
                TransactionCount = transactionCount,
                SubmittedAt = now
            };
            db.StaffCashUps.Add(existing);
        }
        else
        {
            existing.StaffName = staffName;
            existing.CurrencyCode = targetCurrencyCode;
            existing.CashTotal = RoundMoney(cash);
            existing.CardTotal = RoundMoney(card);
            existing.EcoCashTotal = RoundMoney(ecoCash);
            existing.Total = RoundMoney(total);
            existing.TransactionCount = transactionCount;
            existing.SubmittedAt = now;
        }

        await AddRetailAuditAsync(
            $"Cash up submitted by {staffName} for {periodStartUtc:yyyy-MM-dd} totalling {RoundMoney(total):0.00} {targetCurrencyCode}.",
            staffName,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return MapStaffCashUp(existing);
    }

    public async Task<IReadOnlyList<StaffCashUpDto>> GetCashUpsAsync(
        DateTime? from,
        DateTime? to,
        int? staffUserId,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        var query = db.StaffCashUps.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            query = query.Where(x => x.BusinessDate >= fromDate);
        }

        if (to.HasValue)
        {
            var toDate = to.Value.Date;
            query = query.Where(x => x.BusinessDate <= toDate);
        }

        if (staffUserId.HasValue && staffUserId.Value > 0)
        {
            query = query.Where(x => x.StaffUserId == staffUserId.Value);
        }

        if (TryNormalizeCurrencyCode(currency, out var normalizedCurrency))
        {
            query = query.Where(x => x.CurrencyCode == normalizedCurrency);
        }

        var entries = await query
            .OrderByDescending(x => x.BusinessDate)
            .ThenByDescending(x => x.SubmittedAt)
            .Take(800)
            .ToListAsync(cancellationToken);

        return entries.Select(MapStaffCashUp).ToList();
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
        var paymentCurrencyCode = NormalizeCurrencyCode(request.CurrencyCode);
        var exchangeRateToUsd = ResolveExchangeRateToUsd(paymentCurrencyCode);
        if (request.Cart.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        var productIds = request.Cart.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToListAsync(cancellationToken);

        var lineCalculations = new List<(
            Product Product,
            int Quantity,
            decimal UnitPrice,
            decimal Discount,
            decimal TaxableSubtotal,
            decimal Tax,
            decimal LineTotal)>();
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
            lineCalculations.Add((
                product,
                qty,
                RoundMoney(product.Price),
                RoundMoney(totals.Discount),
                RoundMoney(totals.TaxableSubtotal),
                RoundMoney(totals.Tax),
                RoundMoney(totals.TaxableSubtotal + totals.Tax)));
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

        var totalBase = Math.Max(0, gross - pointsRedeemed * PointValue);
        var pointsEarned = (int)Math.Floor(totalBase / 12m);

        foreach (var line in lineCalculations)
        {
            line.Product.Stock = Math.Max(0, line.Product.Stock - line.Quantity);
        }

        var transactionId = BuildTransactionId("tx");
        var now = DateTime.UtcNow;
        var roundedTotalBase = RoundMoney(totalBase);
        var convertedSubtotal = RoundMoney(ConvertFromBaseCurrency(subtotal, exchangeRateToUsd));
        var convertedTax = RoundMoney(ConvertFromBaseCurrency(tax, exchangeRateToUsd));
        var convertedDiscount = RoundMoney(ConvertFromBaseCurrency(discount, exchangeRateToUsd));
        var convertedTotal = RoundMoney(ConvertFromBaseCurrency(totalBase, exchangeRateToUsd));
        int? checkoutStaffId = request.StaffUserId.HasValue && request.StaffUserId.Value > 0
            ? request.StaffUserId.Value
            : null;
        var checkoutStaffName = string.IsNullOrWhiteSpace(request.StaffDisplayName)
            ? null
            : request.StaffDisplayName.Trim();

        var payment = new PosPayment
        {
            ExternalTransactionId = transactionId,
            Method = method,
            CurrencyCode = paymentCurrencyCode,
            ExchangeRateToUsd = exchangeRateToUsd,
            StaffUserId = checkoutStaffId,
            ProcessedByName = checkoutStaffName,
            CustomerPhone = customer?.PhoneNumber ?? customerPhone,
            CustomerName = customer?.Name,
            Subtotal = convertedSubtotal,
            Tax = convertedTax,
            Discount = convertedDiscount,
            PointsRedeemed = pointsRedeemed,
            PointsEarned = pointsEarned,
            Amount = convertedTotal,
            Timestamp = now,
            Lines = lineCalculations.Select(line => new PosPaymentLine
            {
                ProductId = line.Product.Id,
                ProductName = line.Product.Name,
                Sku = line.Product.Sku,
                Quantity = line.Quantity,
                UnitPrice = RoundMoney(ConvertFromBaseCurrency(line.UnitPrice, exchangeRateToUsd)),
                Discount = RoundMoney(ConvertFromBaseCurrency(line.Discount, exchangeRateToUsd)),
                Tax = RoundMoney(ConvertFromBaseCurrency(line.Tax, exchangeRateToUsd)),
                LineTotal = RoundMoney(ConvertFromBaseCurrency(line.LineTotal, exchangeRateToUsd))
            }).ToList()
        };

        db.PosPayments.Add(payment);

        if (customer is not null)
        {
            customer.LoyaltyPoints = customer.LoyaltyPoints - pointsRedeemed + pointsEarned;
            customer.PurchaseHistory.Add(new CustomerPurchaseRecord
            {
                ExternalTransactionId = transactionId,
                Date = now,
                Total = roundedTotalBase,
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
                Sales = roundedTotalBase
            });
        }
        else
        {
            slot.Sales = RoundMoney(slot.Sales + totalBase);
        }

        var role = string.IsNullOrWhiteSpace(request.UserRole) ? (await GetOrCreateSettingsAsync(cancellationToken)).ActiveRole : request.UserRole.Trim();
        await AddRetailAuditAsync(
            $"Checkout {transactionId} completed ({request.PaymentMethod}) for {convertedTotal:0.00} {paymentCurrencyCode}.",
            role,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var roundedTotals = new CartTotalsDto
        {
            Subtotal = convertedSubtotal,
            Tax = convertedTax,
            Discount = convertedDiscount,
            PointsRedeemed = pointsRedeemed,
            Total = convertedTotal,
            PointsEarned = pointsEarned
        };

        return new CheckoutResponseDto
        {
            Success = true,
            TransactionId = transactionId,
            Message = $"Checkout complete. Transaction ID: {transactionId}.",
            Totals = roundedTotals,
            Receipt = new ReceiptPayloadDto
            {
                TransactionId = transactionId,
                Timestamp = now.ToString("O"),
                PaymentMethod = method.ToString(),
                CurrencyCode = paymentCurrencyCode,
                ExchangeRateToUsd = exchangeRateToUsd,
                CustomerName = customer?.Name ?? "Walk-in Customer",
                CustomerPhone = customer?.PhoneNumber ?? customerPhone,
                QrToken = BuildReceiptQrToken(transactionId),
                Totals = roundedTotals,
                LineItems = lineCalculations.Select(line => new ReceiptLineItemDto
                {
                    ProductId = line.Product.Id,
                    ProductName = line.Product.Name,
                    Sku = line.Product.Sku,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    Discount = line.Discount,
                    Tax = line.Tax,
                    LineTotal = line.LineTotal
                }).ToList()
            },
            Bootstrap = await GetBootstrapAsync(cancellationToken)
        };
    }

    public async Task<ReceiptPayloadDto?> GetReceiptByTransactionIdAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTransactionId = (transactionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTransactionId))
        {
            return null;
        }

        var payment = await db.PosPayments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.ExternalTransactionId == normalizedTransactionId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var totals = new CartTotalsDto
        {
            Subtotal = RoundMoney(payment.Subtotal),
            Tax = RoundMoney(payment.Tax),
            Discount = RoundMoney(payment.Discount),
            PointsRedeemed = payment.PointsRedeemed,
            Total = RoundMoney(payment.Amount),
            PointsEarned = payment.PointsEarned
        };

        return new ReceiptPayloadDto
        {
            TransactionId = payment.ExternalTransactionId,
            Timestamp = payment.Timestamp.ToString("O"),
            PaymentMethod = payment.Method.ToString(),
            CurrencyCode = NormalizeCurrencyCode(payment.CurrencyCode),
            ExchangeRateToUsd = NormalizeExchangeRateToUsd(payment.ExchangeRateToUsd),
            CustomerName = string.IsNullOrWhiteSpace(payment.CustomerName) ? "Walk-in Customer" : payment.CustomerName,
            CustomerPhone = payment.CustomerPhone ?? string.Empty,
            QrToken = BuildReceiptQrToken(payment.ExternalTransactionId),
            Totals = totals,
            LineItems = payment.Lines
                .OrderBy(x => x.Id)
                .Select(MapReceiptLine)
            .ToList()
        };
    }

    public Task<ReceiptQrTokenDto?> GetReceiptQrTokenAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTransactionId = (transactionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTransactionId))
        {
            return Task.FromResult<ReceiptQrTokenDto?>(null);
        }

        return Task.FromResult<ReceiptQrTokenDto?>(new ReceiptQrTokenDto
        {
            TransactionId = normalizedTransactionId,
            Token = BuildReceiptQrToken(normalizedTransactionId)
        });
    }

    public async Task<ReceiptVerificationResultDto> VerifyReceiptAsync(
        string transactionId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalizedTransactionId = (transactionId ?? string.Empty).Trim();
        var normalizedToken = (token ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedTransactionId) || string.IsNullOrWhiteSpace(normalizedToken))
        {
            return new ReceiptVerificationResultDto
            {
                ReceiptFound = false,
                IsGenuine = false,
                TransactionId = normalizedTransactionId,
                Message = "Transaction ID and QR token are required for verification."
            };
        }

        var payment = await db.PosPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExternalTransactionId == normalizedTransactionId, cancellationToken);

        if (payment is null)
        {
            return new ReceiptVerificationResultDto
            {
                ReceiptFound = false,
                IsGenuine = false,
                TransactionId = normalizedTransactionId,
                Message = "Receipt was not found in the transaction records."
            };
        }

        var expectedToken = BuildReceiptQrToken(normalizedTransactionId);
        var validToken = ConstantTimeEquals(expectedToken, normalizedToken);

        return new ReceiptVerificationResultDto
        {
            ReceiptFound = true,
            IsGenuine = validToken,
            TransactionId = normalizedTransactionId,
            Message = validToken
                ? "Receipt is genuine."
                : "Receipt record exists, but the QR token does not match.",
            PaymentMethod = payment.Method.ToString(),
            Timestamp = payment.Timestamp.ToString("O"),
            Total = RoundMoney(payment.Amount)
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
        if (TryParsePaymentMethod(paymentMethod, out var method))
        {
            return method;
        }

        throw new InvalidOperationException("Invalid payment method.");
    }

    private static bool TryParsePaymentMethod(string? paymentMethod, out PosPaymentMethod method)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            method = PosPaymentMethod.Card;
            return false;
        }

        switch (paymentMethod.Trim().ToLowerInvariant())
        {
            case "cash":
                method = PosPaymentMethod.Cash;
                return true;
            case "card":
                method = PosPaymentMethod.Card;
                return true;
            case "ecocash":
            case "eco cash":
            case "eco-cash":
            case "digital":
                method = PosPaymentMethod.EcoCash;
                return true;
            default:
                method = PosPaymentMethod.Card;
                return false;
        }
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

    private static bool IsAdminRole(string userRole)
    {
        var role = (userRole ?? string.Empty).Trim();
        return role.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("administrator", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeVendorDepartments(IEnumerable<string>? rawDepartments)
    {
        if (rawDepartments is null)
        {
            return [];
        }

        return rawDepartments
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateOnly? ParseDateOnlyOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateOnly.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static DateOnly ParseDateOnlyOrDefault(string? raw, DateOnly fallback)
    {
        if (DateOnly.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static DateTime ParseBusinessDateOrUtcToday(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.UtcNow.Date;
        }

        if (DateOnly.TryParse(raw, out var dateOnly))
        {
            return DateTime.SpecifyKind(dateOnly.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        }

        if (DateTime.TryParse(raw, out var parsed))
        {
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }

        return DateTime.UtcNow.Date;
    }

    private static bool TryNormalizeCurrencyCode(string? rawCurrencyCode, out string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(rawCurrencyCode))
        {
            currencyCode = BaseCurrencyCode;
            return false;
        }

        var normalized = rawCurrencyCode.Trim().ToUpperInvariant();
        if (SupportedCurrencyRates.ContainsKey(normalized))
        {
            currencyCode = normalized;
            return true;
        }

        currencyCode = BaseCurrencyCode;
        return false;
    }

    private static string NormalizeCurrencyCode(string? rawCurrencyCode)
    {
        return TryNormalizeCurrencyCode(rawCurrencyCode, out var currencyCode)
            ? currencyCode
            : BaseCurrencyCode;
    }

    private static decimal ResolveExchangeRateToUsd(string currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        return SupportedCurrencyRates.TryGetValue(normalized, out var rate) && rate > 0
            ? rate
            : 1m;
    }

    private static decimal NormalizeExchangeRateToUsd(decimal exchangeRateToUsd)
    {
        return exchangeRateToUsd > 0m ? exchangeRateToUsd : 1m;
    }

    private static decimal ConvertFromBaseCurrency(decimal amountInUsd, decimal exchangeRateToUsd)
    {
        return amountInUsd * NormalizeExchangeRateToUsd(exchangeRateToUsd);
    }

    private static decimal ConvertToBaseCurrency(PosPayment payment)
    {
        var normalizedRate = NormalizeExchangeRateToUsd(payment.ExchangeRateToUsd);
        return payment.Amount / normalizedRate;
    }

    private static string NormalizePhone(string phone) => string.Concat(phone.Where(char.IsDigit));

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string BuildTransactionId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..22];

    private string BuildReceiptQrToken(string transactionId)
    {
        var secret = ResolveReceiptTokenSecret();
        var payload = Encoding.UTF8.GetBytes(transactionId.Trim());
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = hmac.ComputeHash(payload);
        return Base64UrlEncode(digest);
    }

    private string ResolveReceiptTokenSecret()
    {
        var configured =
            configuration[ReceiptVerificationSecretConfigPath]
            ?? configuration[JwtSecretConfigPath];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return "SMS-Receipt-Verification-Default-Secret-2026";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static PaymentTrackingRecordDto MapPaymentTracking(PosPayment payment) => new()
    {
        TransactionId = payment.ExternalTransactionId,
        Timestamp = payment.Timestamp.ToString("O"),
        PaymentMethod = payment.Method.ToString(),
        CurrencyCode = NormalizeCurrencyCode(payment.CurrencyCode),
        CustomerName = payment.CustomerName ?? string.Empty,
        CustomerPhone = payment.CustomerPhone ?? string.Empty,
        Subtotal = RoundMoney(payment.Subtotal),
        Tax = RoundMoney(payment.Tax),
        Discount = RoundMoney(payment.Discount),
        PointsRedeemed = payment.PointsRedeemed,
        PointsEarned = payment.PointsEarned,
        Total = RoundMoney(payment.Amount),
        ItemCount = payment.Lines.Sum(x => x.Quantity),
        LineItems = payment.Lines
            .OrderBy(x => x.Id)
            .Select(MapReceiptLine)
            .ToList()
    };

    private static StaffCashUpDto MapStaffCashUp(StaffCashUp cashUp) => new()
    {
        Id = cashUp.Id,
        StaffUserId = cashUp.StaffUserId,
        StaffName = cashUp.StaffName,
        CurrencyCode = NormalizeCurrencyCode(cashUp.CurrencyCode),
        BusinessDate = cashUp.BusinessDate.ToString("yyyy-MM-dd"),
        CashTotal = RoundMoney(cashUp.CashTotal),
        CardTotal = RoundMoney(cashUp.CardTotal),
        EcoCashTotal = RoundMoney(cashUp.EcoCashTotal),
        Total = RoundMoney(cashUp.Total),
        TransactionCount = cashUp.TransactionCount,
        SubmittedAt = cashUp.SubmittedAt.ToString("O")
    };

    private static ReceiptLineItemDto MapReceiptLine(PosPaymentLine line) => new()
    {
        ProductId = line.ProductId,
        ProductName = line.ProductName,
        Sku = line.Sku,
        Quantity = line.Quantity,
        UnitPrice = RoundMoney(line.UnitPrice),
        Discount = RoundMoney(line.Discount),
        Tax = RoundMoney(line.Tax),
        LineTotal = RoundMoney(line.LineTotal)
    };

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
        ArrivalDate = product.ArrivalDate?.ToString("yyyy-MM-dd"),
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
