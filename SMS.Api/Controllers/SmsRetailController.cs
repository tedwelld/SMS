using Microsoft.AspNetCore.Mvc;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api")]
public class SmsRetailController(ISmsRetailService service) : ControllerBase
{
    [HttpGet("bootstrap")]
    public async Task<ActionResult<BootstrapPayloadDto>> Bootstrap(CancellationToken cancellationToken)
        => Ok(await service.GetBootstrapAsync(cancellationToken));

    [HttpPatch("settings")]
    public async Task<ActionResult<SmsAppSettingsDto>> PatchSettings([FromBody] PatchSettingsRequestDto request, CancellationToken cancellationToken)
        => Ok(await service.PatchSettingsAsync(request, cancellationToken));

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetProducts([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await service.GetProductsAsync(search, cancellationToken));

    [HttpPatch("products/{productId}/physical-count")]
    public async Task<ActionResult<object>> UpdatePhysicalCount(
        string productId,
        [FromBody] UpdatePhysicalCountRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.PhysicalCount < 0)
        {
            return BadRequest(new { message = "Invalid physical count." });
        }

        try
        {
            var result = await service.UpdatePhysicalCountAsync(productId, request.PhysicalCount, ResolveRole(), cancellationToken);
            return Ok(new
            {
                product = result.Product,
                draftPurchaseOrders = result.DraftPurchaseOrders
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
    }

    [HttpPatch("products/{productId}/promotion")]
    public async Task<ActionResult<object>> UpdatePromotion(
        string productId,
        [FromBody] UpdatePromotionRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedType = request.Type.Trim().ToLowerInvariant();
        if (normalizedType is not ("none" or "discount" or "bogo"))
        {
            return BadRequest(new { message = "Invalid promotion type." });
        }

        try
        {
            var product = await service.UpdatePromotionAsync(productId, request, ResolveRole(), cancellationToken);
            return Ok(new { product });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerProfileDto>>> GetCustomers([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await service.GetCustomersAsync(search, cancellationToken));

    [HttpGet("customers/by-phone/{phone}")]
    public async Task<ActionResult<CustomerProfileDto>> GetCustomerByPhone(string phone, CancellationToken cancellationToken)
    {
        var customer = await service.GetCustomerByPhoneAsync(phone, cancellationToken);
        return customer is null ? NotFound(new { message = "Customer not found." }) : Ok(customer);
    }

    [HttpPost("customers")]
    public async Task<ActionResult<CustomerProfileDto>> AddCustomer([FromBody] CreateRetailCustomerRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await service.AddCustomerAsync(request, ResolveRole(), cancellationToken);
            return Created($"/api/customers/{customer.Id}", customer);
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase) ? 409 : 400;
            return StatusCode(status, new { message = ex.Message });
        }
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<IReadOnlyList<VendorDto>>> GetVendors(CancellationToken cancellationToken)
        => Ok(await service.GetVendorsAsync(cancellationToken));

    [HttpGet("purchase-orders/drafts")]
    public async Task<ActionResult<IReadOnlyList<DraftPurchaseOrderDto>>> GetDraftPurchaseOrders(CancellationToken cancellationToken)
        => Ok(await service.GetDraftPurchaseOrdersAsync(cancellationToken));

    [HttpPost("purchase-orders/drafts/regenerate")]
    public async Task<ActionResult<IReadOnlyList<DraftPurchaseOrderDto>>> RegenerateDraftPurchaseOrders(CancellationToken cancellationToken)
        => Ok(await service.RegenerateDraftPurchaseOrdersAsync(ResolveRole(), cancellationToken));

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<RetailAuditLogDto>>> GetAudit([FromQuery] int? limit, CancellationToken cancellationToken)
        => Ok(await service.GetAuditAsync(limit ?? 20, cancellationToken));

    [HttpGet("reports/eod")]
    public async Task<ActionResult<EodReportDto>> GetEod(CancellationToken cancellationToken)
        => Ok(await service.GetEodReportAsync(cancellationToken));

    [HttpGet("reports/shrinkage")]
    public async Task<ActionResult<IReadOnlyList<ShrinkageReportRowDto>>> GetShrinkage(CancellationToken cancellationToken)
        => Ok(await service.GetShrinkageReportAsync(cancellationToken));

    [HttpGet("reports/sales-trends")]
    public async Task<ActionResult<IReadOnlyList<SalesTrendPointDto>>> GetSalesTrend(CancellationToken cancellationToken)
        => Ok(await service.GetSalesTrendAsync(cancellationToken));

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponseDto>> Checkout([FromBody] CheckoutRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.CheckoutAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string ResolveRole()
    {
        if (Request.Headers.TryGetValue("X-User-Role", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return "Store Manager";
    }
}
