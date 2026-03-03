using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/wallets")]
public class WalletsController(IWalletService service, SmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WalletDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WalletDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WalletDto>> Create([FromBody] CreateWalletRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("by-customer")]
    public async Task<ActionResult<WalletDto>> CreateByCustomer(
        [FromBody] CreateWalletForCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0)
        {
            return BadRequest(new { message = "A valid customerId is required." });
        }

        if (request.OpeningBalance < 0m)
        {
            return BadRequest(new { message = "Opening balance cannot be negative." });
        }

        var customerExists = await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            return BadRequest(new { message = $"Customer {request.CustomerId} does not exist." });
        }

        var customerAccount = await db.CustomerAccounts
            .Include(x => x.Wallets)
            .FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId, cancellationToken);

        if (customerAccount is null)
        {
            customerAccount = new Data.EntityModels.CustomerAccount
            {
                CustomerId = request.CustomerId,
                AccountNumber = await GenerateAccountNumberAsync(request.CustomerId, cancellationToken),
                Balance = request.OpeningBalance,
                IsFrozen = false
            };

            db.CustomerAccounts.Add(customerAccount);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingWallet = customerAccount.Wallets.FirstOrDefault();
        if (existingWallet is not null)
        {
            return Ok(new WalletDto(
                existingWallet.Id,
                existingWallet.CustomerAccountId,
                existingWallet.IsActive,
                existingWallet.DateCreated));
        }

        var wallet = new Data.EntityModels.Wallet
        {
            CustomerAccountId = customerAccount.Id,
            IsActive = true,
            DateCreated = DateTime.UtcNow
        };

        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);

        var dto = new WalletDto(wallet.Id, wallet.CustomerAccountId, wallet.IsActive, wallet.DateCreated);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<object>> UpdateStatus(
        int id,
        [FromBody] UpdateWalletStatusRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets
            .Include(x => x.CustomerAccount)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (wallet?.CustomerAccount is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var normalizedStatus = request.Status.Trim().ToLowerInvariant();
        switch (normalizedStatus)
        {
            case "active":
                wallet.IsActive = true;
                wallet.CustomerAccount.IsFrozen = false;
                break;
            case "suspended":
                wallet.IsActive = true;
                wallet.CustomerAccount.IsFrozen = true;
                break;
            case "closed":
                wallet.IsActive = false;
                wallet.CustomerAccount.IsFrozen = true;
                break;
            default:
                return BadRequest(new { message = "Status must be active, suspended, or closed." });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WalletDto>> UpdateWallet(
        int id,
        [FromBody] UpdateWalletRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets
            .Include(x => x.CustomerAccount)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (wallet?.CustomerAccount is null)
        {
            return NotFound();
        }

        if (request.Balance.HasValue && request.Balance.Value < 0m)
        {
            return BadRequest(new { message = "Balance cannot be negative." });
        }

        if (request.IsActive.HasValue)
        {
            wallet.IsActive = request.IsActive.Value;
        }

        if (request.IsFrozen.HasValue)
        {
            wallet.CustomerAccount.IsFrozen = request.IsFrozen.Value;
        }

        if (request.Balance.HasValue)
        {
            wallet.CustomerAccount.Balance = request.Balance.Value;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new WalletDto(wallet.Id, wallet.CustomerAccountId, wallet.IsActive, wallet.DateCreated));
    }

    public sealed class UpdateWalletStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public sealed class CreateWalletForCustomerRequest
    {
        public int CustomerId { get; set; }
        public decimal OpeningBalance { get; set; }
    }

    public sealed class UpdateWalletRequest
    {
        public decimal? Balance { get; set; }
        public bool? IsFrozen { get; set; }
        public bool? IsActive { get; set; }
    }

    private async Task<string> GenerateAccountNumberAsync(int customerId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var accountNumber = $"ACC-{customerId:D6}-{Random.Shared.Next(1000, 9999)}";
            var exists = await db.CustomerAccounts.AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken);
            if (!exists)
            {
                return accountNumber;
            }
        }

        return $"ACC-{customerId:D6}-{DateTime.UtcNow:HHmmssfff}";
    }
}


