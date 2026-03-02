using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Infrastructure;
using SMS.Core.Dtos;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize(Roles = "User")]
public class UserPortalController(SmsDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<UserDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var transactions = await db.WalletTransactions.AsNoTracking()
            .Where(x => x.CustomerAccountId == context.Account.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(80)
            .Select(x => new WalletTransactionDto
            {
                Id = x.Id,
                CustomerAccountId = x.CustomerAccountId,
                TransactionType = x.TransactionType,
                Channel = x.Channel,
                Amount = x.Amount,
                BalanceAfter = x.BalanceAfter,
                CounterpartyPhoneNumber = x.CounterpartyPhoneNumber,
                Reference = x.Reference,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var sms = await db.SmsNotifications.AsNoTracking()
            .Where(x => x.CustomerId == context.Customer.Id)
            .OrderByDescending(x => x.SentAt)
            .Take(40)
            .Select(x => new SmsNotificationDto(x.Id, x.CustomerId, x.SentAt, x.IsSuccess, x.Message))
            .ToListAsync(cancellationToken);

        var notifications = await db.AccountNotifications.AsNoTracking()
            .Where(x => x.CustomerId == context.Customer.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(60)
            .Select(x => new AccountNotificationDto
            {
                Id = x.Id,
                NotificationType = x.NotificationType,
                Title = x.Title,
                Message = x.Message,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new UserDashboardDto
        {
            Profile = new UserProfileDto
            {
                CustomerId = context.Customer.Id,
                Name = context.Customer.Name,
                PhoneNumber = context.Customer.PhoneNumber,
                Email = context.Customer.Email,
                IsActive = context.Customer.IsActive,
                ProfileImageUrl = context.Customer.ProfileImageUrl
            },
            Wallet = new WalletAccessDto
            {
                WalletId = context.Wallet.Id,
                CustomerAccountId = context.Account.Id,
                AccountNumber = context.Account.AccountNumber,
                Balance = context.Account.Balance,
                IsFrozen = context.Account.IsFrozen,
                NfcCardUid = context.NfcCard.CardUid,
                NfcLinkedPhoneNumber = context.NfcCard.PhoneNumber
            },
            Transactions = transactions,
            SmsNotifications = sms,
            Notifications = notifications,
            UnreadNotifications = notifications.Count(x => !x.IsRead)
        });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> GetTransactions([FromQuery] string? transactionType, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var query = db.WalletTransactions.AsNoTracking()
            .Where(x => x.CustomerAccountId == context.Account.Id);

        if (!string.IsNullOrWhiteSpace(transactionType) && !string.Equals(transactionType, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedType = transactionType.Trim().ToLowerInvariant();
            query = query.Where(x => x.TransactionType.ToLower() == normalizedType);
        }

        var transactions = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WalletTransactionDto
            {
                Id = x.Id,
                CustomerAccountId = x.CustomerAccountId,
                CustomerId = context.Customer.Id,
                CustomerName = context.Customer.Name,
                CustomerPhoneNumber = context.Customer.PhoneNumber,
                TransactionType = x.TransactionType,
                Channel = x.Channel,
                Amount = x.Amount,
                BalanceAfter = x.BalanceAfter,
                CounterpartyPhoneNumber = x.CounterpartyPhoneNumber,
                Reference = x.Reference,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpGet("transactions/export")]
    public async Task<ActionResult<UserTransactionExportDto>> ExportTransactions([FromQuery] string? transactionType, CancellationToken cancellationToken)
    {
        var transactions = await GetTransactions(transactionType, cancellationToken);
        if (transactions.Result is UnauthorizedObjectResult)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var type = string.IsNullOrWhiteSpace(transactionType) ? "all" : transactionType.Trim();
        return Ok(new UserTransactionExportDto
        {
            TransactionType = type,
            Transactions = transactions.Value ?? []
        });
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<AccountNotificationDto>>> GetNotifications(CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var notifications = await db.AccountNotifications.AsNoTracking()
            .Where(x => x.CustomerId == context.Customer.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(120)
            .Select(x => new AccountNotificationDto
            {
                Id = x.Id,
                NotificationType = x.NotificationType,
                Title = x.Title,
                Message = x.Message,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    [HttpPut("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(int id, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var notification = await db.AccountNotifications
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == context.Customer.Id, cancellationToken);
        if (notification is null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Notification marked as read." });
    }

    [HttpGet("communications")]
    public async Task<ActionResult<IReadOnlyList<CommunicationMessageDto>>> GetCommunications(CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var customerId = context.Customer.Id;
        var items = await db.CommunicationMessages.AsNoTracking()
            .Where(x =>
                (x.SenderCustomerId == customerId && !x.DeletedBySender)
                || (x.RecipientCustomerId == customerId && !x.DeletedByRecipient))
            .OrderByDescending(x => x.CreatedAt)
            .Take(250)
            .Select(x => new CommunicationMessageDto
            {
                Id = x.Id,
                Subject = x.Subject,
                Message = x.Message,
                SenderType = x.SenderType,
                SenderCustomerId = x.SenderCustomerId,
                SenderStaffUserId = x.SenderStaffUserId,
                RecipientType = x.RecipientType,
                RecipientCustomerId = x.RecipientCustomerId,
                RecipientStaffUserId = x.RecipientStaffUserId,
                Direction = x.SenderCustomerId == customerId ? "Sent" : "Received",
                CounterpartyName = x.SenderCustomerId == customerId
                    ? (x.RecipientType == "Admin"
                        ? (x.RecipientStaffUser != null ? x.RecipientStaffUser.Name : "Admin")
                        : (x.RecipientCustomer != null ? x.RecipientCustomer.Name : "User"))
                    : (x.SenderType == "Admin"
                        ? (x.SenderStaffUser != null ? x.SenderStaffUser.Name : "Admin")
                        : (x.SenderCustomer != null ? x.SenderCustomer.Name : "User")),
                CounterpartyPhoneNumber = x.SenderCustomerId == customerId
                    ? (x.RecipientType == "Admin"
                        ? (x.RecipientStaffUser != null ? x.RecipientStaffUser.PhoneNumber ?? string.Empty : string.Empty)
                        : (x.RecipientCustomer != null ? x.RecipientCustomer.PhoneNumber : string.Empty))
                    : (x.SenderType == "Admin"
                        ? (x.SenderStaffUser != null ? x.SenderStaffUser.PhoneNumber ?? string.Empty : string.Empty)
                        : (x.SenderCustomer != null ? x.SenderCustomer.PhoneNumber : string.Empty)),
                IsReadByRecipient = x.IsReadByRecipient,
                ReadAt = x.ReadAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("communications")]
    public async Task<ActionResult<CommunicationMessageDto>> CreateCommunication([FromBody] CreateCommunicationMessageRequest request, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var subject = request.Subject?.Trim();
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Subject and message are required." });
        }

        var recipientType = string.Equals(request.RecipientType?.Trim(), "User", StringComparison.OrdinalIgnoreCase)
            ? "User"
            : "Admin";

        Customer? targetCustomer = null;
        StaffUser? targetAdmin = null;

        if (recipientType == "User")
        {
            if (request.RecipientCustomerId.HasValue)
            {
                targetCustomer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.RecipientCustomerId.Value, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(request.RecipientPhoneNumber))
            {
                var normalizedPhone = UserOnboardingService.NormalizePhone(request.RecipientPhoneNumber);
                targetCustomer = await db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken);
            }

            if (targetCustomer is null)
            {
                return BadRequest(new { message = "Target user was not found." });
            }
        }
        else
        {
            if (request.RecipientStaffUserId.HasValue)
            {
                targetAdmin = await db.StaffUsers.FirstOrDefaultAsync(
                    x => x.Id == request.RecipientStaffUserId.Value && x.IsActive,
                    cancellationToken);
            }

            if (targetAdmin is null && !string.IsNullOrWhiteSpace(request.RecipientPhoneNumber))
            {
                var normalizedAdminPhone = UserOnboardingService.NormalizePhone(request.RecipientPhoneNumber);
                targetAdmin = await db.StaffUsers.FirstOrDefaultAsync(
                    x => x.PhoneNumber == normalizedAdminPhone && x.IsActive,
                    cancellationToken);
            }

            targetAdmin ??= await db.StaffUsers
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

            if (targetAdmin is null)
            {
                return BadRequest(new { message = "No active admin recipient is available." });
            }
        }

        var entry = new CommunicationMessage
        {
            Subject = subject,
            Message = message,
            SenderType = "User",
            SenderCustomerId = context.Customer.Id,
            SenderStaffUserId = null,
            RecipientType = recipientType,
            RecipientCustomerId = targetCustomer?.Id,
            RecipientStaffUserId = targetAdmin?.Id,
            IsReadByRecipient = false,
            CreatedAt = DateTime.UtcNow
        };

        db.CommunicationMessages.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync(
            "User",
            context.Customer.Id,
            "CreateCommunication",
            "CommunicationMessage",
            entry.Id.ToString(),
            $"User sent communication '{subject}' to {recipientType}.",
            cancellationToken);

        return Ok(new CommunicationMessageDto
        {
            Id = entry.Id,
            Subject = entry.Subject,
            Message = entry.Message,
            SenderType = entry.SenderType,
            SenderCustomerId = entry.SenderCustomerId,
            SenderStaffUserId = entry.SenderStaffUserId,
            RecipientType = entry.RecipientType,
            RecipientCustomerId = entry.RecipientCustomerId,
            RecipientStaffUserId = entry.RecipientStaffUserId,
            Direction = "Sent",
            CounterpartyName = recipientType == "Admin" ? targetAdmin?.Name ?? "Admin" : targetCustomer?.Name ?? "User",
            CounterpartyPhoneNumber = recipientType == "Admin" ? targetAdmin?.PhoneNumber ?? string.Empty : targetCustomer?.PhoneNumber ?? string.Empty,
            IsReadByRecipient = entry.IsReadByRecipient,
            ReadAt = entry.ReadAt,
            CreatedAt = entry.CreatedAt
        });
    }

    [HttpPut("communications/{id:int}/read")]
    public async Task<IActionResult> MarkCommunicationAsRead(int id, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var entry = await db.CommunicationMessages
            .FirstOrDefaultAsync(x => x.Id == id && x.RecipientCustomerId == context.Customer.Id && !x.DeletedByRecipient, cancellationToken);

        if (entry is null)
        {
            return NotFound(new { message = "Communication message not found." });
        }

        if (!entry.IsReadByRecipient)
        {
            entry.IsReadByRecipient = true;
            entry.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Communication marked as read." });
    }

    [HttpDelete("communications/{id:int}")]
    public async Task<IActionResult> DeleteCommunication(int id, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var entry = await db.CommunicationMessages.FirstOrDefaultAsync(
            x => x.Id == id && (x.SenderCustomerId == context.Customer.Id || x.RecipientCustomerId == context.Customer.Id),
            cancellationToken);

        if (entry is null)
        {
            return NotFound(new { message = "Communication message not found." });
        }

        if (entry.SenderCustomerId == context.Customer.Id)
        {
            entry.DeletedBySender = true;
        }

        if (entry.RecipientCustomerId == context.Customer.Id)
        {
            entry.DeletedByRecipient = true;
        }

        if (entry.DeletedBySender && entry.DeletedByRecipient)
        {
            db.CommunicationMessages.Remove(entry);
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync(
            "User",
            context.Customer.Id,
            "DeleteCommunication",
            "CommunicationMessage",
            id.ToString(),
            "User deleted communication message.",
            cancellationToken);
        return NoContent();
    }

    [HttpPost("deposit")]
    public async Task<ActionResult<WalletBalanceDto>> Deposit([FromBody] WalletOperationRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        context.Account.Balance += request.Amount;
        var reference = BuildReference("DEP", request.Reference);

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "Deposit",
            Channel = NormalizeChannel(request.Channel),
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            CounterpartyPhoneNumber = null,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = context.Customer.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"Deposit of {request.Amount:0.00} was successful. Ref: {reference}."
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "Deposit Successful", $"Deposit of {request.Amount:0.00} posted. Ref: {reference}.", "Deposit", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "Deposit", "WalletTransaction", reference, $"Deposit amount {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("withdraw")]
    public async Task<ActionResult<WalletBalanceDto>> Withdraw([FromBody] WalletOperationRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        if (context.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds." });
        }

        context.Account.Balance -= request.Amount;
        var reference = BuildReference("WDR", request.Reference);

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "Withdrawal",
            Channel = NormalizeChannel(request.Channel),
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            CounterpartyPhoneNumber = null,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = context.Customer.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"Withdrawal of {request.Amount:0.00} was successful. Ref: {reference}."
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "Withdrawal Posted", $"Withdrawal of {request.Amount:0.00} posted. Ref: {reference}.", "Withdrawal", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "Withdraw", "WalletTransaction", reference, $"Withdrawal amount {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<WalletBalanceDto>> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        if (context.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds." });
        }

        var todayStart = DateTime.UtcNow.Date;
        var todaysTransfers = await db.WalletTransactions.AsNoTracking()
            .Where(x => x.CustomerAccountId == context.Account.Id
                        && x.TransactionType == "TransferOut"
                        && x.CreatedAt >= todayStart)
            .SumAsync(x => x.Amount, cancellationToken);
        if (todaysTransfers + request.Amount > context.Customer.DailyTransferLimit)
        {
            return BadRequest(new { message = $"Daily transfer limit exceeded ({context.Customer.DailyTransferLimit:0.00})." });
        }

        var targetPhone = UserOnboardingService.NormalizePhone(request.TargetPhoneNumber);
        if (targetPhone == context.Customer.PhoneNumber)
        {
            return BadRequest(new { message = "Cannot transfer to your own account." });
        }

        var target = await db.Customers
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.PhoneNumber == targetPhone, cancellationToken);

        if (target?.Account is null)
        {
            return NotFound(new { message = "Target account was not found." });
        }

        var reference = BuildReference("TRF", null);

        context.Account.Balance -= request.Amount;
        target.Account.Balance += request.Amount;

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "TransferOut",
            Channel = "InternalTransfer",
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            CounterpartyPhoneNumber = targetPhone,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = target.Account.Id,
            TransactionType = "TransferIn",
            Channel = "InternalTransfer",
            Amount = request.Amount,
            BalanceAfter = target.Account.Balance,
            CounterpartyPhoneNumber = context.Customer.PhoneNumber,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = context.Customer.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"Transfer of {request.Amount:0.00} to {targetPhone} succeeded. Ref: {reference}."
        });
        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = target.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"You received {request.Amount:0.00} from {context.Customer.PhoneNumber}. Ref: {reference}."
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "Transfer Sent", $"Transfer of {request.Amount:0.00} to {targetPhone} succeeded. Ref: {reference}.", "Transfer", cancellationToken);
        await CreateAccountNotificationAsync(target.Id, "Transfer Received", $"You received {request.Amount:0.00} from {context.Customer.PhoneNumber}. Ref: {reference}.", "Transfer", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "TransferOut", "WalletTransaction", reference, $"Transfer out to {targetPhone} amount {request.Amount:0.00}", cancellationToken);
        await RecordSnapshotAsync("User", target.Id, "TransferIn", "WalletTransaction", reference, $"Transfer in from {context.Customer.PhoneNumber} amount {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("generate-qr")]
    public async Task<ActionResult<UserQrCodeDto>> GenerateQr([FromBody] GenerateQrRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var tokenValue = Guid.NewGuid().ToString("N");
        var pin = Random.Shared.Next(1000, 9999).ToString();
        var expiry = DateTime.UtcNow.AddMinutes(20);
        var provider = NormalizeChannel(request.Provider);

        db.QrTokens.Add(new QrToken
        {
            WalletId = context.Wallet.Id,
            Token = tokenValue,
            Expiry = expiry,
            MaxUsage = 1,
            CurrentUsage = 0,
            Pin = pin
        });

        await db.SaveChangesAsync(cancellationToken);

        var payload =
            $"TAR_DIGITAL_WALLET|provider={provider}|phone={context.Customer.PhoneNumber}|amount={request.Amount:0.00}|token={tokenValue}|pin={pin}|account={context.Account.AccountNumber}";

        return Ok(new UserQrCodeDto
        {
            Provider = provider,
            Token = tokenValue,
            Expiry = expiry,
            Amount = request.Amount,
            Payload = payload,
            Pin = pin
        });
    }

    [HttpPost("deposit/nfc")]
    public async Task<ActionResult<WalletBalanceDto>> DepositByNfc([FromBody] NfcDepositRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var normalizedCard = request.CardUid.Trim();
        if (!string.Equals(normalizedCard, context.NfcCard.CardUid, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "NFC card does not match the current account." });
        }

        context.Account.Balance += request.Amount;
        var reference = BuildReference("NFCDEP", null);
        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "NfcDeposit",
            Channel = NormalizeChannel(request.Channel),
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "NFC Deposit Posted", $"NFC deposit of {request.Amount:0.00} posted. Ref: {reference}.", "Deposit", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "NfcDeposit", "WalletTransaction", reference, $"NFC deposit {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("deposit/qr-confirm")]
    public async Task<ActionResult<WalletBalanceDto>> ConfirmQrDeposit([FromBody] QrDepositRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var token = await db.QrTokens.FirstOrDefaultAsync(x => x.Token == request.Token.Trim(), cancellationToken);
        if (token is null
            || token.WalletId != context.Wallet.Id
            || token.Pin != request.Pin.Trim()
            || token.Expiry < DateTime.UtcNow
            || token.CurrentUsage >= token.MaxUsage)
        {
            return BadRequest(new { message = "QR token is invalid or expired." });
        }

        context.Account.Balance += request.Amount;
        token.CurrentUsage += 1;
        var reference = BuildReference("QRDEP", null);
        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "QrDeposit",
            Channel = NormalizeChannel(request.Channel),
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "QR Deposit Posted", $"QR deposit of {request.Amount:0.00} posted. Ref: {reference}.", "Deposit", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "QrDeposit", "WalletTransaction", reference, $"QR deposit {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("payments/merchant")]
    public async Task<ActionResult<WalletBalanceDto>> MerchantPayment([FromBody] MerchantTillPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        if (context.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds." });
        }

        context.Account.Balance -= request.Amount;
        var reference = BuildReference("MRCH", null);
        db.MerchantTillPayments.Add(new MerchantTillPayment
        {
            CustomerAccountId = context.Account.Id,
            MerchantName = request.MerchantName.Trim(),
            TillNumber = request.TillNumber.Trim(),
            Amount = request.Amount,
            Reference = reference,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "MerchantPayment",
            Channel = "MerchantTill",
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "Merchant Payment", $"Payment to {request.MerchantName.Trim()} of {request.Amount:0.00} succeeded. Ref: {reference}.", "Payment", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "MerchantPayment", "WalletTransaction", reference, $"Merchant payment {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("payments/bill")]
    public async Task<ActionResult<WalletBalanceDto>> BillPayment([FromBody] BillPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        if (context.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds." });
        }

        context.Account.Balance -= request.Amount;
        var reference = BuildReference("BILL", null);
        db.BillPayments.Add(new BillPayment
        {
            CustomerAccountId = context.Account.Id,
            BillerCode = request.BillerCode.Trim(),
            BillerName = request.BillerName.Trim(),
            AccountReference = request.AccountReference.Trim(),
            Amount = request.Amount,
            Reference = reference,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "BillPayment",
            Channel = request.BillerCode.Trim(),
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(context.Customer.Id, "Bill Payment", $"Bill payment to {request.BillerName.Trim()} of {request.Amount:0.00} succeeded. Ref: {reference}.", "Payment", cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "BillPayment", "WalletTransaction", reference, $"Bill payment {request.Amount:0.00}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = context.Account.Balance });
    }

    [HttpPost("purchases/airtime")]
    public async Task<ActionResult<AirtimePurchaseResultDto>> BuyAirtime([FromBody] AirtimePurchaseRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (context.Account.IsFrozen)
        {
            return BadRequest(new { message = "Account is frozen." });
        }

        if (context.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds." });
        }

        var network = NormalizeNetwork(request.Network);
        if (string.IsNullOrWhiteSpace(network))
        {
            return BadRequest(new { message = "Unsupported airtime network." });
        }

        var targetPhoneNumber = UserOnboardingService.NormalizePhone(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(targetPhoneNumber))
        {
            return BadRequest(new { message = "Valid target phone number is required." });
        }

        var existingAirtimeBalance = await db.AirtimePurchases.AsNoTracking()
            .Where(x => x.PhoneNumber == targetPhoneNumber
                        && x.Network == network
                        && x.Status == "Completed")
            .SumAsync(x => x.Amount, cancellationToken);

        var airtimeBalanceAfter = existingAirtimeBalance + request.Amount;
        context.Account.Balance -= request.Amount;
        var reference = BuildReference("AIR", null);
        var voucher = $"{network[..Math.Min(network.Length, 3)]}-{Random.Shared.Next(100000, 999999)}-{Random.Shared.Next(1000, 9999)}";

        db.AirtimePurchases.Add(new AirtimePurchase
        {
            CustomerAccountId = context.Account.Id,
            Network = network,
            PhoneNumber = targetPhoneNumber,
            Amount = request.Amount,
            AirtimeBalanceAfter = airtimeBalanceAfter,
            VoucherCode = voucher,
            Reference = reference,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        });

        db.WalletTransactions.Add(new WalletTransaction
        {
            CustomerAccountId = context.Account.Id,
            TransactionType = "AirtimePurchase",
            Channel = network,
            Amount = request.Amount,
            BalanceAfter = context.Account.Balance,
            Reference = reference,
            Notes = $"Voucher: {voucher}",
            CreatedAt = DateTime.UtcNow
        });

        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = context.Customer.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"Airtime purchase successful. {network} {request.Amount:0.00} for {targetPhoneNumber}. Voucher: {voucher}. Airtime balance: {airtimeBalanceAfter:0.00}. Ref: {reference}."
        });

        var targetCustomer = await db.Customers
            .FirstOrDefaultAsync(x => x.PhoneNumber == targetPhoneNumber, cancellationToken);
        if (targetCustomer is not null && targetCustomer.Id != context.Customer.Id)
        {
            db.SmsNotifications.Add(new SmsNotification
            {
                CustomerId = targetCustomer.Id,
                SentAt = DateTime.UtcNow,
                IsSuccess = true,
                Message = $"You received airtime voucher {voucher} ({network} {request.Amount:0.00}). Balance: {airtimeBalanceAfter:0.00}. Ref: {reference}."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(
            context.Customer.Id,
            "Airtime Purchased",
            $"Airtime purchase ({network}) of {request.Amount:0.00} for {targetPhoneNumber} succeeded. Voucher: {voucher}. Airtime balance: {airtimeBalanceAfter:0.00}.",
            "Airtime",
            cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "AirtimePurchase", "WalletTransaction", reference, $"Airtime purchase {request.Amount:0.00} for {network}", cancellationToken);
        return Ok(new AirtimePurchaseResultDto
        {
            WalletBalance = context.Account.Balance,
            AirtimeBalanceAfter = airtimeBalanceAfter,
            VoucherCode = voucher,
            Network = network,
            PhoneNumber = targetPhoneNumber,
            Reference = reference
        });
    }

    [HttpPost("support/report")]
    public async Task<ActionResult<IssueCommunicationResultDto>> ReportIssue([FromBody] IssueCommunicationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Issue message is required." });
        }

        if (!request.SendSms && !request.SendEmail)
        {
            return BadRequest(new { message = "Select at least one channel (SMS or email)." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject) ? "Wallet Issue Report" : request.Subject.Trim();
        var targetPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? context.Customer.PhoneNumber : UserOnboardingService.NormalizePhone(request.PhoneNumber);
        var targetEmail = string.IsNullOrWhiteSpace(request.Email) ? context.Customer.Email : request.Email.Trim().ToLowerInvariant();

        if (request.SendSms)
        {
            db.SmsNotifications.Add(new SmsNotification
            {
                CustomerId = context.Customer.Id,
                SentAt = DateTime.UtcNow,
                IsSuccess = true,
                Message = $"Issue SMS queued to {targetPhone}. Subject: {subject}. Message: {request.Message.Trim()}"
            });
        }

        if (request.SendEmail)
        {
            db.AccountNotifications.Add(new AccountNotification
            {
                CustomerId = context.Customer.Id,
                NotificationType = "SupportEmail",
                Title = "Issue Email Queued",
                Message = $"Issue email queued to {targetEmail}. Subject: {subject}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync(
            "User",
            context.Customer.Id,
            "SupportReport",
            "Customer",
            context.Customer.Id.ToString(),
            $"Issue report submitted via {(request.SendSms && request.SendEmail ? "SMS+Email" : request.SendSms ? "SMS" : "Email")}. Subject: {subject}",
            cancellationToken);

        return Ok(new IssueCommunicationResultDto
        {
            SmsQueued = request.SendSms,
            EmailQueued = request.SendEmail,
            TargetPhoneNumber = request.SendSms ? targetPhone : string.Empty,
            TargetEmail = request.SendEmail ? targetEmail : string.Empty,
            Summary = "Issue report was queued successfully."
        });
    }

    [HttpPut("settings/profile")]
    public async Task<ActionResult<UserProfileDto>> UpdateSettings([FromBody] UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        context.Customer.Name = request.Name.Trim();
        context.Customer.Email = request.Email.Trim().ToLowerInvariant();
        context.Customer.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        context.Customer.NotificationsEnabled = request.NotificationsEnabled;
        context.Customer.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage);
        if (request.DailyTransferLimit > 0)
        {
            context.Customer.DailyTransferLimit = request.DailyTransferLimit;
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync("User", context.Customer.Id, "UpdateProfile", "Customer", context.Customer.Id.ToString(), "Updated profile/settings.", cancellationToken);

        return Ok(new UserProfileDto
        {
            CustomerId = context.Customer.Id,
            Name = context.Customer.Name,
            PhoneNumber = context.Customer.PhoneNumber,
            Email = context.Customer.Email,
            IsActive = context.Customer.IsActive,
            ProfileImageUrl = context.Customer.ProfileImageUrl
        });
    }

    [HttpPut("settings/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "New password must be at least 6 characters long." });
        }

        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        if (string.IsNullOrWhiteSpace(context.Customer.PasswordHash)
            || !context.Customer.PasswordHash.StartsWith("$2")
            || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, context.Customer.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        context.Customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword.Trim());
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password updated." });
    }

    private async Task<UserContext?> GetUserContextAsync(CancellationToken cancellationToken)
    {
        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(customerIdClaim, out var customerId))
        {
            return null;
        }

        var customer = await db.Customers
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .ThenInclude(x => x.NfcCards)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        var account = customer?.Account;
        var wallet = account?.Wallets.FirstOrDefault();
        var nfc = wallet?.NfcCards.FirstOrDefault();

        if (customer is null || account is null || wallet is null || nfc is null)
        {
            return null;
        }

        return new UserContext(customer, account, wallet, nfc);
    }

    private static string NormalizeChannel(string channel)
    {
        var value = channel.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Bank";
        }

        return value;
    }

    private static string BuildReference(string prefix, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested.Trim();
        }

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        var normalized = language.Trim().ToLowerInvariant();
        return normalized is "en" or "fr" or "pt" or "sw"
            ? normalized
            : "en";
    }

    private static string NormalizeNetwork(string network)
    {
        var normalized = network.Trim().ToLowerInvariant();
        return normalized switch
        {
            "econet" => "Econet",
            "netone" => "NetOne",
            "mtn" => "MTN",
            "telkom" => "Telkom",
            "vodacom" => "Vodacom",
            "liquid" => "Liquid",
            _ => string.Empty
        };
    }

    private async Task CreateAccountNotificationAsync(
        int customerId,
        string title,
        string message,
        string notificationType,
        CancellationToken cancellationToken)
    {
        db.AccountNotifications.Add(new AccountNotification
        {
            CustomerId = customerId,
            NotificationType = notificationType,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordSnapshotAsync(
        string actorType,
        int? actorId,
        string action,
        string entityType,
        string? entityId,
        string details,
        CancellationToken cancellationToken)
    {
        db.ActivitySnapshots.Add(new ActivitySnapshot
        {
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record UserContext(Customer Customer, CustomerAccount Account, Wallet Wallet, NfcCard NfcCard);
}


