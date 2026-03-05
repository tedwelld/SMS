using SMS.Core.Dtos;

namespace SMS.Core.Interfaces;

public interface ISmsRetailService
{
    Task<BootstrapPayloadDto> GetBootstrapAsync(CancellationToken cancellationToken = default);
    Task<SmsAppSettingsDto> PatchSettingsAsync(PatchSettingsRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(string productId, string userRole, CancellationToken cancellationToken = default);
    Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdatePhysicalCountAsync(
        string productId,
        int physicalCount,
        string userRole,
        CancellationToken cancellationToken = default);
    Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdateStockAsync(
        string productId,
        int quantity,
        string mode,
        string userRole,
        CancellationToken cancellationToken = default);
    Task<ProductDto> UpdatePromotionAsync(string productId, UpdatePromotionRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerProfileDto>> GetCustomersAsync(string? search, CancellationToken cancellationToken = default);
    Task<CustomerProfileDto?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<CustomerProfileDto> AddCustomerAsync(CreateRetailCustomerRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken cancellationToken = default);
    Task<VendorDto> CreateVendorAsync(CreateVendorRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<VendorDto> UpdateVendorAsync(int vendorId, UpdateVendorRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task DeleteVendorAsync(int vendorId, string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftPurchaseOrderDto>> GetDraftPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftPurchaseOrderDto>> RegenerateDraftPurchaseOrdersAsync(string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetailAuditLogDto>> GetAuditAsync(int limit, CancellationToken cancellationToken = default);
    Task<EodReportDto> GetEodReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTrackingRecordDto>> GetPaymentsAsync(
        DateTime? from,
        DateTime? to,
        string? method,
        string? query,
        string? currency,
        int limit,
        CancellationToken cancellationToken = default);
    Task<StaffCashUpDto> SubmitDailyCashUpAsync(SubmitCashUpRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffCashUpDto>> GetCashUpsAsync(
        DateTime? from,
        DateTime? to,
        int? staffUserId,
        string? currency,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShrinkageReportRowDto>> GetShrinkageReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(CancellationToken cancellationToken = default);
    Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default);
    Task<ReceiptPayloadDto?> GetReceiptByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<ReceiptQrTokenDto?> GetReceiptQrTokenAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<ReceiptVerificationResultDto> VerifyReceiptAsync(string transactionId, string token, CancellationToken cancellationToken = default);
}
