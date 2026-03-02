using SMS.Core.Dtos;

namespace SMS.Core.Interfaces;

public interface ISmsRetailService
{
    Task<BootstrapPayloadDto> GetBootstrapAsync(CancellationToken cancellationToken = default);
    Task<SmsAppSettingsDto> PatchSettingsAsync(PatchSettingsRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? search, CancellationToken cancellationToken = default);
    Task<(ProductDto Product, IReadOnlyList<DraftPurchaseOrderDto> DraftPurchaseOrders)> UpdatePhysicalCountAsync(
        string productId,
        int physicalCount,
        string userRole,
        CancellationToken cancellationToken = default);
    Task<ProductDto> UpdatePromotionAsync(string productId, UpdatePromotionRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerProfileDto>> GetCustomersAsync(string? search, CancellationToken cancellationToken = default);
    Task<CustomerProfileDto?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<CustomerProfileDto> AddCustomerAsync(CreateRetailCustomerRequestDto request, string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftPurchaseOrderDto>> GetDraftPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftPurchaseOrderDto>> RegenerateDraftPurchaseOrdersAsync(string userRole, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetailAuditLogDto>> GetAuditAsync(int limit, CancellationToken cancellationToken = default);
    Task<EodReportDto> GetEodReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShrinkageReportRowDto>> GetShrinkageReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(CancellationToken cancellationToken = default);
    Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default);
}
