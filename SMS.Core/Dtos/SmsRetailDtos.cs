namespace SMS.Core.Dtos;

public sealed class SmsAppSettingsDto
{
    public List<string> Roles { get; set; } = [];
    public string ActiveRole { get; set; } = "Store Manager";
    public bool OfflineMode { get; set; }
}

public sealed class PromotionDto
{
    public string Type { get; set; } = "none";
    public decimal Value { get; set; }
}

public sealed class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; }
    public decimal TaxRate { get; set; }
    public bool Staple { get; set; }
    public string? ExpiryDate { get; set; }
    public PromotionDto Promo { get; set; } = new();
    public int PhysicalCount { get; set; }
}

public sealed class PurchaseRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int PointsEarned { get; set; }
}

public sealed class CustomerProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int Points { get; set; }
    public List<PurchaseRecordDto> PurchaseHistory { get; set; } = [];
}

public sealed class VendorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Departments { get; set; } = [];
    public int LeadTimeDays { get; set; }
    public string Contact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class PurchaseOrderLineDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int SuggestedOrderQty { get; set; }
}

public sealed class DraftPurchaseOrderDto
{
    public string Id { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<PurchaseOrderLineDto> Lines { get; set; } = [];
}

public sealed class EodReportDto
{
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public decimal Digital { get; set; }
    public decimal Total { get; set; }
    public int Transactions { get; set; }
}

public sealed class ShrinkageReportRowDto
{
    public string Product { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int SystemStock { get; set; }
    public int PhysicalCount { get; set; }
    public int Variance { get; set; }
}

public sealed class SalesTrendPointDto
{
    public string Hour { get; set; } = string.Empty;
    public decimal Sales { get; set; }
}

public sealed class ReportsPayloadDto
{
    public EodReportDto Eod { get; set; } = new();
    public List<ShrinkageReportRowDto> Shrinkage { get; set; } = [];
}

public sealed class RetailAuditLogDto
{
    public int Id { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public sealed class BootstrapPayloadDto
{
    public SmsAppSettingsDto Settings { get; set; } = new();
    public List<ProductDto> Products { get; set; } = [];
    public List<CustomerProfileDto> Customers { get; set; } = [];
    public List<VendorDto> Vendors { get; set; } = [];
    public List<DraftPurchaseOrderDto> DraftPurchaseOrders { get; set; } = [];
    public List<SalesTrendPointDto> SalesTrend { get; set; } = [];
    public ReportsPayloadDto Reports { get; set; } = new();
    public List<RetailAuditLogDto> AuditLogs { get; set; } = [];
}

public sealed class CheckoutLineItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class CheckoutRequestDto
{
    public List<CheckoutLineItemDto> Cart { get; set; } = [];
    public string PaymentMethod { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int PointsToRedeem { get; set; }
    public string UserRole { get; set; } = string.Empty;
}

public sealed class CartTotalsDto
{
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public int PointsRedeemed { get; set; }
    public decimal Total { get; set; }
    public int PointsEarned { get; set; }
}

public sealed class CheckoutResponseDto
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public CartTotalsDto Totals { get; set; } = new();
    public BootstrapPayloadDto Bootstrap { get; set; } = new();
}

public sealed class PatchSettingsRequestDto
{
    public string? ActiveRole { get; set; }
    public bool? OfflineMode { get; set; }
}

public sealed class UpdatePhysicalCountRequestDto
{
    public int PhysicalCount { get; set; }
}

public sealed class UpdatePromotionRequestDto
{
    public string Type { get; set; } = "none";
    public decimal Value { get; set; }
}

public sealed class CreateRetailCustomerRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequestDto
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class CreateStaffAccountRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
}
