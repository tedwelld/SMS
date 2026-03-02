using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(SmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortalTransactionDto>>> GetAll(
        [FromQuery] string? walletId,
        CancellationToken cancellationToken)
    {
        var query = BuildTransactionsQuery(walletId);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapToPortalTransaction).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PortalTransactionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await BuildTransactionsQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return item is null
            ? NotFound(new { message = "Transaction was not found." })
            : Ok(MapToPortalTransaction(item));
    }

    [HttpPost("filter")]
    public async Task<ActionResult<IReadOnlyList<PortalTransactionDto>>> Filter(
        [FromBody] TransactionFilterRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildTransactionsQuery(request.WalletId);

        if (request.StartDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            var inclusiveEnd = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.CreatedAt <= inclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var expectedType = MapFrontendToDomainType(NormalizeFrontendType(request.Type));
            query = query.Where(x => x.TransactionType == expectedType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !string.Equals(request.Status.Trim(), "success", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(Array.Empty<PortalTransactionDto>());
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapToPortalTransaction).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PortalTransactionDto>> Create(
        [FromBody] CreatePortalTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WalletId))
        {
            return BadRequest(new { message = "walletId is required." });
        }

        var wallet = await ResolveWalletAsync(request.WalletId.Trim(), cancellationToken);
        if (wallet?.CustomerAccount is null)
        {
            return BadRequest(new { message = "Wallet was not found." });
        }

        var frontendType = NormalizeFrontendType(request.Type);
        var outflow = IsFrontendOutflow(frontendType);
        var fee = Math.Max(0m, request.Fee);

        var signedAmount = request.Amount != 0m
            ? request.Amount
            : outflow
                ? -Math.Abs(request.Total)
                : Math.Abs(request.Total);

        var amountMagnitude = Math.Abs(signedAmount);
        if (amountMagnitude <= 0m)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var signedTotal = request.Total != 0m
            ? request.Total
            : outflow
                ? -(amountMagnitude + fee)
                : amountMagnitude;

        var totalMagnitude = Math.Abs(signedTotal);
        if (totalMagnitude <= 0m)
        {
            totalMagnitude = amountMagnitude;
        }

        var account = wallet.CustomerAccount;
        if (account.IsFrozen)
        {
            return BadRequest(new { message = "Wallet account is frozen." });
        }

        if (outflow && account.Balance < totalMagnitude)
        {
            return BadRequest(new { message = "Insufficient balance." });
        }

        account.Balance += outflow ? -totalMagnitude : totalMagnitude;

        var entity = new WalletTransaction
        {
            CustomerAccountId = account.Id,
            TransactionType = MapFrontendToDomainType(frontendType),
            Channel = NormalizeChannel(frontendType),
            Amount = totalMagnitude,
            BalanceAfter = account.Balance,
            CounterpartyPhoneNumber = null,
            Reference = BuildReference(frontendType, request.Reference),
            Notes = BuildNotes(request.Description, request.MerchantName, request.StaffName, fee),
            CreatedAt = DateTime.UtcNow
        };

        db.WalletTransactions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new PortalTransactionDto
        {
            Id = entity.Id.ToString(),
            Reference = entity.Reference,
            WalletId = wallet.Id.ToString(),
            WalletOwner = account.Customer?.Name ?? string.Empty,
            Type = frontendType,
            Amount = signedAmount,
            Fee = fee,
            Total = signedTotal,
            Currency = "USD",
            Status = "success",
            Timestamp = entity.CreatedAt,
            MerchantName = string.IsNullOrWhiteSpace(request.MerchantName) ? null : request.MerchantName.Trim(),
            StaffId = request.StaffId,
            StaffName = request.StaffName,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"{frontendType} transaction"
                : request.Description.Trim()
        });
    }

    private IQueryable<WalletTransaction> BuildTransactionsQuery(string? walletId = null)
    {
        var query = db.WalletTransactions.AsNoTracking()
            .Include(x => x.CustomerAccount)
                .ThenInclude(x => x.Customer)
            .Include(x => x.CustomerAccount)
                .ThenInclude(x => x.Wallets)
                .ThenInclude(x => x.NfcCards)
            .AsQueryable();

        if (string.IsNullOrWhiteSpace(walletId))
        {
            return query;
        }

        var candidate = walletId.Trim();
        if (int.TryParse(candidate, out var walletKey))
        {
            return query.Where(x => x.CustomerAccount.Wallets.Any(w => w.Id == walletKey));
        }

        return query.Where(x => x.CustomerAccount.Wallets.Any(w => w.NfcCards.Any(c => c.CardUid == candidate)));
    }

    private async Task<Wallet?> ResolveWalletAsync(string walletId, CancellationToken cancellationToken)
    {
        if (int.TryParse(walletId, out var walletKey))
        {
            return await db.Wallets
                .Include(x => x.CustomerAccount)
                .ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == walletKey, cancellationToken);
        }

        var nfcCard = await db.NfcCards
            .Include(x => x.Wallet)
            .ThenInclude(x => x.CustomerAccount)
            .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.CardUid == walletId, cancellationToken);

        return nfcCard?.Wallet;
    }

    private static PortalTransactionDto MapToPortalTransaction(WalletTransaction source)
    {
        var wallet = source.CustomerAccount?.Wallets.OrderBy(x => x.Id).FirstOrDefault();
        var frontendType = MapDomainToFrontendType(source.TransactionType);
        var signedAmount = IsFrontendOutflow(frontendType) ? -source.Amount : source.Amount;

        return new PortalTransactionDto
        {
            Id = source.Id.ToString(),
            Reference = source.Reference,
            WalletId = wallet?.Id.ToString() ?? source.CustomerAccountId.ToString(),
            WalletOwner = source.CustomerAccount?.Customer?.Name ?? string.Empty,
            Type = frontendType,
            Amount = signedAmount,
            Fee = 0m,
            Total = signedAmount,
            Currency = "USD",
            Status = "success",
            Timestamp = source.CreatedAt,
            MerchantName = ExtractMerchantName(source.Notes),
            StaffId = null,
            StaffName = null,
            Description = string.IsNullOrWhiteSpace(source.Notes)
                ? $"{frontendType} transaction"
                : source.Notes.Trim()
        };
    }

    private static string NormalizeFrontendType(string? type)
    {
        var normalized = type?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "payment" => "payment",
            "deposit" => "deposit",
            "withdrawal" => "withdrawal",
            "transfer" => "transfer",
            "refund" => "refund",
            _ => "payment"
        };
    }

    private static string MapFrontendToDomainType(string frontendType) =>
        frontendType switch
        {
            "deposit" => "Deposit",
            "withdrawal" => "Withdrawal",
            "transfer" => "TransferOut",
            "refund" => "Deposit",
            _ => "MerchantPayment"
        };

    private static string MapDomainToFrontendType(string domainType)
    {
        var normalized = domainType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "deposit" or "cashdeposit" => "deposit",
            "withdrawal" => "withdrawal",
            "transferout" => "transfer",
            "merchantpayment" or "billpayment" or "airtimepurchase" => "payment",
            _ => "payment"
        };
    }

    private static bool IsFrontendOutflow(string frontendType) =>
        frontendType is "payment" or "withdrawal" or "transfer";

    private static string NormalizeChannel(string frontendType) =>
        frontendType switch
        {
            "deposit" => "Cash",
            "withdrawal" => "POS",
            "transfer" => "Transfer",
            _ => "MerchantTill"
        };

    private static string BuildReference(string frontendType, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested.Trim();
        }

        var prefix = frontendType switch
        {
            "deposit" => "DEP",
            "withdrawal" => "WDR",
            "transfer" => "TRF",
            "refund" => "RFD",
            _ => "PAY"
        };

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string BuildNotes(string? description, string? merchantName, string? staffName, decimal fee)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(merchantName))
        {
            parts.Add($"merchant:{merchantName.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(staffName))
        {
            parts.Add($"staff:{staffName.Trim()}");
        }

        if (fee > 0m)
        {
            parts.Add($"fee:{fee:0.00}");
        }

        return parts.Count == 0 ? "POS transaction" : string.Join(" | ", parts);
    }

    private static string? ExtractMerchantName(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        const string prefix = "merchant:";
        foreach (var part in notes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return part[prefix.Length..].Trim();
            }
        }

        return null;
    }

    public sealed class TransactionFilterRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? WalletId { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }

    public sealed class CreatePortalTransactionRequest
    {
        public string WalletId { get; set; } = string.Empty;
        public string? Type { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public decimal Total { get; set; }
        public string? Currency { get; set; }
        public string? MerchantName { get; set; }
        public string? StaffId { get; set; }
        public string? StaffName { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
    }

    public sealed class PortalTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string WalletId { get; set; } = string.Empty;
        public string WalletOwner { get; set; } = string.Empty;
        public string Type { get; set; } = "payment";
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; } = "USD";
        public string Status { get; set; } = "success";
        public DateTime Timestamp { get; set; }
        public string? MerchantName { get; set; }
        public string? StaffId { get; set; }
        public string? StaffName { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}


