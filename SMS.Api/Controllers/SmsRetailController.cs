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

    [HttpPost("products")]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await service.CreateProductAsync(request, ResolveRole(), cancellationToken);
            return Created($"/api/products/{product.Id}", product);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase) ? 409 : 400;
            return StatusCode(status, new { message = ex.Message });
        }
    }

    [HttpDelete("products/{productId}")]
    public async Task<ActionResult<object>> DeleteProduct(string productId, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteProductAsync(productId, ResolveRole(), cancellationToken);
            return Ok(new { message = "Product deleted." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
    }

    [HttpPut("products/{productId}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        string productId,
        [FromBody] UpdateProductRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await service.UpdateProductAsync(productId, request, ResolveRole(), cancellationToken);
            return Ok(product);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase) ? 409 : 400;
            return StatusCode(status, new { message = ex.Message });
        }
    }

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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
    }

    [HttpPatch("products/{productId}/stock")]
    public async Task<ActionResult<object>> UpdateStock(
        string productId,
        [FromBody] UpdateStockRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity < 0)
        {
            return BadRequest(new { message = "Invalid stock quantity." });
        }

        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "set" : request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("set" or "add"))
        {
            return BadRequest(new { message = "Mode must be 'set' or 'add'." });
        }

        try
        {
            var result = await service.UpdateStockAsync(productId, request.Quantity, mode, ResolveRole(), cancellationToken);
            return Ok(new
            {
                product = result.Product,
                draftPurchaseOrders = result.DraftPurchaseOrders
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found." });
        }
    }

    [HttpGet("retail/customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerProfileDto>>> GetCustomers([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await service.GetCustomersAsync(search, cancellationToken));

    [HttpGet("retail/customers/by-phone/{phone}")]
    public async Task<ActionResult<CustomerProfileDto>> GetCustomerByPhone(string phone, CancellationToken cancellationToken)
    {
        var customer = await service.GetCustomerByPhoneAsync(phone, cancellationToken);
        return customer is null ? NotFound(new { message = "Customer not found." }) : Ok(customer);
    }

    [HttpPost("retail/customers")]
    public async Task<ActionResult<CustomerProfileDto>> AddCustomer([FromBody] CreateRetailCustomerRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await service.AddCustomerAsync(request, ResolveRole(), cancellationToken);
            return Created($"/api/retail/customers/{customer.Id}", customer);
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
    {
        try
        {
            return Ok(await service.RegenerateDraftPurchaseOrdersAsync(ResolveRole(), cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<RetailAuditLogDto>>> GetAudit([FromQuery] int? limit, CancellationToken cancellationToken)
        => Ok(await service.GetAuditAsync(limit ?? 20, cancellationToken));

    [HttpGet("reports/eod")]
    public async Task<ActionResult<EodReportDto>> GetEod(CancellationToken cancellationToken)
        => Ok(await service.GetEodReportAsync(cancellationToken));

    [HttpGet("payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentTrackingRecordDto>>> GetPayments(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? method,
        [FromQuery] string? query,
        [FromQuery] string? currency,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Ok(await service.GetPaymentsAsync(from, to, method, query, currency, limit ?? 100, cancellationToken));

    [HttpPost("cash-ups/submit")]
    [HttpPost("cashups/submit")]
    public async Task<ActionResult<StaffCashUpDto>> SubmitCashUp([FromBody] SubmitCashUpRequestDto request, CancellationToken cancellationToken)
    {
        if (request.StaffUserId <= 0)
        {
            return BadRequest(new { message = "A valid staff user id is required." });
        }

        try
        {
            return Ok(await service.SubmitDailyCashUpAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("cash-ups")]
    [HttpGet("cashups")]
    public async Task<ActionResult<IReadOnlyList<StaffCashUpDto>>> GetCashUps(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? staffUserId,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => Ok(await service.GetCashUpsAsync(from, to, staffUserId, currency, cancellationToken));

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

    [HttpGet("receipts/{transactionId}/verify")]
    public async Task<ActionResult<ReceiptVerificationResultDto>> VerifyReceipt(
        string transactionId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await service.VerifyReceiptAsync(transactionId, token, cancellationToken);
        return Ok(result);
    }

    [HttpGet("receipts/{transactionId}")]
    public async Task<ActionResult<ReceiptPayloadDto>> GetReceipt(string transactionId, CancellationToken cancellationToken)
    {
        var receipt = await service.GetReceiptByTransactionIdAsync(transactionId, cancellationToken);
        if (receipt is null)
        {
            return NotFound(new { message = "Receipt not found." });
        }

        return Ok(receipt);
    }

    [HttpGet("receipts/{transactionId}/qr-token")]
    public async Task<ActionResult<ReceiptQrTokenDto>> GetReceiptQrToken(string transactionId, CancellationToken cancellationToken)
    {
        var payload = await service.GetReceiptQrTokenAsync(transactionId, cancellationToken);
        if (payload is null)
        {
            return BadRequest(new { message = "Transaction ID is required." });
        }

        return Ok(payload);
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
