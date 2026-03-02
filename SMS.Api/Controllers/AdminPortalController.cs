using System.Security.Claims;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Infrastructure;
using SMS.Core.Dtos;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Staff")]
public class AdminPortalController(SmsDbContext db, UserOnboardingService onboardingService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(
        [FromQuery] string? range,
        [FromQuery] int? userId,
        [FromQuery] string? phoneNumber,
        [FromQuery] string? name,
        [FromQuery] string? transactionType,
        [FromQuery] string? channel,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var filters = BuildFilters(range, userId, phoneNumber, name, transactionType, channel, startUtc, endUtc);
        return Ok(await BuildAdminDashboardAsync(filters, cancellationToken));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await db.Customers
            .AsNoTracking()
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .ThenInclude(x => x.NfcCards)
            .ToListAsync(cancellationToken);

        return Ok(users.Select(MapUser).ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateUser([FromBody] AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var onboarded = await onboardingService.CreateUserAsync(
                request.PhoneNumber,
                request.Name,
                request.Email,
                request.Password,
                request.OpeningBalance,
                request.IsFrozen,
                cancellationToken);

            await LogAuditAsync("CreateUser", $"Created user {onboarded.Customer.PhoneNumber}", onboarded.Wallet.Id, cancellationToken);
            await CreateAccountNotificationAsync(onboarded.Customer.Id, "Profile Created", "Your Tar Digital Wallet account has been created by a staff member.", "Profile", null, cancellationToken);
            await RecordSnapshotAsync("Admin", "CreateUser", "Customer", onboarded.Customer.Id.ToString(), $"Created user {onboarded.Customer.PhoneNumber}", cancellationToken);
            return CreatedAtAction(nameof(GetUsers), new { }, MapUserFromOnboarded(onboarded));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/by-phone/{phoneNumber}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(string phoneNumber, [FromBody] AdminUpdateUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedPhone = UserOnboardingService.NormalizePhone(phoneNumber);
        var user = await db.Customers
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .ThenInclude(x => x.NfcCards)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken);

        if (user?.Account is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var newPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? user.PhoneNumber
            : UserOnboardingService.NormalizePhone(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(newPhone))
        {
            return BadRequest(new { message = "Valid phone number is required." });
        }

        if (!string.Equals(newPhone, user.PhoneNumber, StringComparison.Ordinal))
        {
            var phoneUsed = await db.Customers.AnyAsync(x => x.PhoneNumber == newPhone && x.Id != user.Id, cancellationToken);
            if (phoneUsed)
            {
                return BadRequest(new { message = "Phone number already exists." });
            }
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailUsed = await db.Customers.AnyAsync(
            x => x.Email == normalizedEmail && x.Id != user.Id,
            cancellationToken);
        if (emailUsed)
        {
            return BadRequest(new { message = "Email already exists." });
        }

        user.Name = request.Name.Trim();
        user.Email = normalizedEmail;
        user.PhoneNumber = newPhone;
        user.IsActive = request.IsActive;
        user.NotificationsEnabled = request.NotificationsEnabled;
        user.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage);
        if (request.DailyTransferLimit > 0)
        {
            user.DailyTransferLimit = request.DailyTransferLimit;
        }
        user.Account.IsFrozen = request.IsFrozen;
        var nfcCard = user.Account.Wallets.FirstOrDefault()?.NfcCards.FirstOrDefault();
        if (nfcCard is not null)
        {
            nfcCard.PhoneNumber = newPhone;
        }

        await db.SaveChangesAsync(cancellationToken);
        await LogAuditAsync("UpdateUser", $"Updated user {user.PhoneNumber}", user.Account.Wallets.FirstOrDefault()?.Id, cancellationToken);
        await CreateAccountNotificationAsync(user.Id, "Profile Updated", "Your profile/settings were updated by administrator.", "Profile", null, cancellationToken);
        await RecordSnapshotAsync("Admin", "UpdateUser", "Customer", user.Id.ToString(), $"Updated user {user.PhoneNumber}", cancellationToken);
        return Ok(MapUser(user));
    }

    [HttpDelete("users/by-phone/{phoneNumber}")]
    public async Task<IActionResult> DeleteUser(string phoneNumber, CancellationToken cancellationToken)
    {
        var normalizedPhone = UserOnboardingService.NormalizePhone(phoneNumber);
        var user = await db.Customers
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var walletId = user.Account?.Wallets.FirstOrDefault()?.Id;
        db.Customers.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        await LogAuditAsync("DeleteUser", $"Deleted user {normalizedPhone}", walletId, cancellationToken);
        await RecordSnapshotAsync("Admin", "DeleteUser", "Customer", user.Id.ToString(), $"Deleted user {normalizedPhone}", cancellationToken);
        return NoContent();
    }

    [HttpPost("users/{customerId:int}/nfc-cards")]
    public async Task<ActionResult<AdminIssueNfcCardResultDto>> IssueNfcCard(
        int customerId,
        [FromBody] AdminIssueNfcCardRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .ThenInclude(x => x.NfcCards)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return NotFound(new { message = $"Customer {customerId} was not found." });
        }

        if (request.QrTokenExpiryDays <= 0 || request.QrTokenExpiryDays > 365)
        {
            return BadRequest(new { message = "QrTokenExpiryDays must be between 1 and 365." });
        }

        if (request.QrTokenMaxUsage <= 0 || request.QrTokenMaxUsage > 100000)
        {
            return BadRequest(new { message = "QrTokenMaxUsage must be between 1 and 100000." });
        }

        var now = DateTime.UtcNow;
        var account = customer.Account;
        if (account is null)
        {
            account = new CustomerAccount
            {
                CustomerId = customer.Id,
                AccountNumber = await GenerateUniqueAccountNumberAsync(customer.PhoneNumber, cancellationToken),
                Balance = 0m,
                IsFrozen = false
            };
            db.CustomerAccounts.Add(account);
            await db.SaveChangesAsync(cancellationToken);
        }

        var wallet = await db.Wallets
            .Include(x => x.NfcCards)
            .Include(x => x.AccessMethods)
            .Include(x => x.QrTokens)
            .FirstOrDefaultAsync(x => x.CustomerAccountId == account.Id, cancellationToken);

        if (wallet is null)
        {
            wallet = new Wallet
            {
                CustomerAccountId = account.Id,
                IsActive = true,
                DateCreated = now
            };
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingCard = wallet.NfcCards.OrderByDescending(x => x.Id).FirstOrDefault();
        var requestedCardUid = string.IsNullOrWhiteSpace(request.CardUid)
            ? await GenerateUniqueNfcCardUidAsync(customer.PhoneNumber, cancellationToken)
            : request.CardUid.Trim();

        if (requestedCardUid.Length > 128)
        {
            return BadRequest(new { message = "Card UID must be 128 characters or less." });
        }

        var cardUidInUse = await db.NfcCards.AnyAsync(
            x => x.CardUid == requestedCardUid && (existingCard == null || x.Id != existingCard.Id),
            cancellationToken);
        if (cardUidInUse)
        {
            return BadRequest(new { message = "Card UID is already in use." });
        }

        var replacedExistingCard = false;
        if (existingCard is null)
        {
            db.NfcCards.Add(new NfcCard
            {
                WalletId = wallet.Id,
                CardUid = requestedCardUid,
                PhoneNumber = customer.PhoneNumber
            });
        }
        else
        {
            if (!request.ReplaceExisting)
            {
                return BadRequest(new { message = "Wallet already has an NFC card. Set replaceExisting=true to reissue." });
            }

            existingCard.CardUid = requestedCardUid;
            existingCard.PhoneNumber = customer.PhoneNumber;
            replacedExistingCard = true;
        }

        var accessMethodCreated = false;
        if (request.EnsureAccessMethod && !wallet.AccessMethods.Any())
        {
            db.AccessMethods.Add(new AccessMethod
            {
                WalletId = wallet.Id,
                IsActive = true,
                DateCreated = now
            });
            accessMethodCreated = true;
        }

        var qrTokenCreated = false;
        QrToken? activeToken = null;
        if (request.EnsureQrToken)
        {
            activeToken = wallet.QrTokens
                .Where(x => x.Expiry > now && x.CurrentUsage < x.MaxUsage)
                .OrderByDescending(x => x.Expiry)
                .FirstOrDefault();

            if (activeToken is null)
            {
                activeToken = new QrToken
                {
                    WalletId = wallet.Id,
                    Token = Guid.NewGuid().ToString("N"),
                    Expiry = now.AddDays(request.QrTokenExpiryDays),
                    MaxUsage = request.QrTokenMaxUsage,
                    CurrentUsage = 0,
                    Pin = Random.Shared.Next(1000, 9999).ToString()
                };

                db.QrTokens.Add(activeToken);
                qrTokenCreated = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await LogAuditAsync("IssueNfcCard", $"Issued NFC card for customer {customer.PhoneNumber}.", wallet.Id, cancellationToken);
        await RecordSnapshotAsync("Admin", "IssueNfcCard", "NfcCard", wallet.Id.ToString(), $"Issued card for customer {customer.PhoneNumber}", cancellationToken);

        return Ok(new AdminIssueNfcCardResultDto
        {
            CustomerId = customer.Id,
            WalletId = wallet.Id,
            CardUid = requestedCardUid,
            PhoneNumber = customer.PhoneNumber,
            ReplacedExistingCard = replacedExistingCard,
            AccessMethodCreated = accessMethodCreated,
            QrTokenCreated = qrTokenCreated,
            QrToken = activeToken?.Token,
            QrTokenExpiry = activeToken?.Expiry
        });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> GetTransactions(
        [FromQuery] string? phoneNumber,
        [FromQuery] string? name,
        [FromQuery] int? userId,
        [FromQuery] string? transactionType,
        [FromQuery] string? channel,
        [FromQuery] string? range,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var filters = BuildFilters(range ?? "all", userId, phoneNumber, name, transactionType, channel, startUtc, endUtc);
        var transactions = await BuildFilteredTransactionsQuery(filters)
            .OrderByDescending(x => x.CreatedAt)
            .Select(TransactionProjection())
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpGet("reports/preview")]
    public async Task<ActionResult<AdminReportPreviewDto>> PreviewReport(
        [FromQuery] string? phoneNumber,
        [FromQuery] string? name,
        [FromQuery] int? userId,
        [FromQuery] string? transactionType,
        [FromQuery] string? channel,
        [FromQuery] string? range,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var filters = BuildFilters(range, userId, phoneNumber, name, transactionType, channel, startUtc, endUtc);
        var query = BuildFilteredTransactionsQuery(filters);
        var totalAmount = await query.SumAsync(x => x.Amount, cancellationToken);
        var transactions = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(600)
            .Select(TransactionProjection())
            .ToListAsync(cancellationToken);

        return Ok(new AdminReportPreviewDto
        {
            Filters = filters,
            ResultCount = transactions.Count,
            TotalAmount = totalAmount,
            Transactions = transactions
        });
    }

    [HttpGet("exports")]
    public async Task<ActionResult<AdminExportPayloadDto>> ExportData(
        [FromQuery] string? dataset,
        [FromQuery] string? range,
        [FromQuery] string? phoneNumber,
        [FromQuery] string? name,
        [FromQuery] int? userId,
        [FromQuery] string? transactionType,
        [FromQuery] string? channel,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var filters = BuildFilters(range, userId, phoneNumber, name, transactionType, channel, startUtc, endUtc);
        var normalizedDataset = NormalizeDataset(dataset);
        var dashboard = await BuildAdminDashboardAsync(filters, cancellationToken);
        dashboard = FilterDashboardByDataset(dashboard, normalizedDataset);

        return Ok(new AdminExportPayloadDto
        {
            Dataset = normalizedDataset,
            Filters = filters,
            Dashboard = dashboard
        });
    }

    [HttpPost("transactions/send-trace")]
    public async Task<IActionResult> SendTraceToUser([FromBody] SendTransactionTraceRequest request, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsQueryable();

        if (request.CustomerId.HasValue)
        {
            query = query.Where(x => x.Id == request.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var normalizedPhone = UserOnboardingService.NormalizePhone(request.PhoneNumber);
            query = query.Where(x => x.PhoneNumber == normalizedPhone);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var nameMatch = request.Name.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(nameMatch));
        }

        var users = await query.ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return NotFound(new { message = "No users matched target criteria." });
        }

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "Your requested transaction trace is ready and was sent by admin."
            : request.Message.Trim();

        foreach (var user in users)
        {
            db.AccountNotifications.Add(new AccountNotification
            {
                CustomerId = user.Id,
                NotificationType = "Trace",
                Title = "Transaction Trace Ready",
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            db.SmsNotifications.Add(new SmsNotification
            {
                CustomerId = user.Id,
                SentAt = DateTime.UtcNow,
                IsSuccess = true,
                Message = message
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync("Admin", "SendTrace", "Customer", string.Join(',', users.Select(x => x.Id)), $"Sent trace to {users.Count} users", cancellationToken);
        return Ok(new { sent = users.Count });
    }

    [HttpGet("communications")]
    public async Task<ActionResult<IReadOnlyList<CommunicationMessageDto>>> GetCommunications([FromQuery] string? phoneNumber, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        var adminId = admin.Id;
        var query = db.CommunicationMessages.AsNoTracking()
            .Where(x =>
                (x.SenderStaffUserId == adminId && !x.DeletedBySender)
                || ((x.RecipientStaffUserId == adminId || (x.RecipientType == "Admin" && x.RecipientStaffUserId == null))
                    && !x.DeletedByRecipient));

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var normalizedPhone = UserOnboardingService.NormalizePhone(phoneNumber);
            query = query.Where(x =>
                (x.SenderCustomer != null && x.SenderCustomer.PhoneNumber == normalizedPhone)
                || (x.RecipientCustomer != null && x.RecipientCustomer.PhoneNumber == normalizedPhone));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
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
                Direction = x.SenderStaffUserId == adminId ? "Sent" : "Received",
                CounterpartyName = x.SenderStaffUserId == adminId
                    ? (x.RecipientType == "Admin"
                        ? (x.RecipientStaffUser != null ? x.RecipientStaffUser.Name : "Admin")
                        : (x.RecipientCustomer != null ? x.RecipientCustomer.Name : "User"))
                    : (x.SenderType == "Admin"
                        ? (x.SenderStaffUser != null ? x.SenderStaffUser.Name : "Admin")
                        : (x.SenderCustomer != null ? x.SenderCustomer.Name : "User")),
                CounterpartyPhoneNumber = x.SenderStaffUserId == adminId
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
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        var subject = request.Subject?.Trim();
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Subject and message are required." });
        }

        var recipientType = string.Equals(request.RecipientType?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "User";

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
                .FirstOrDefaultAsync(x => x.IsActive && x.Id != admin.Id, cancellationToken);

            targetAdmin ??= admin;
        }

        var entry = new CommunicationMessage
        {
            Subject = subject,
            Message = message,
            SenderType = "Admin",
            SenderCustomerId = null,
            SenderStaffUserId = admin.Id,
            RecipientType = recipientType,
            RecipientCustomerId = targetCustomer?.Id,
            RecipientStaffUserId = targetAdmin?.Id,
            IsReadByRecipient = false,
            CreatedAt = DateTime.UtcNow
        };

        db.CommunicationMessages.Add(entry);

        if (targetCustomer is not null)
        {
            db.AccountNotifications.Add(new AccountNotification
            {
                CustomerId = targetCustomer.Id,
                NotificationType = "Communication",
                Title = subject,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync(
            "Admin",
            "CreateCommunication",
            "CommunicationMessage",
            entry.Id.ToString(),
            $"Admin sent communication '{subject}' to {recipientType}.",
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
            CounterpartyName = recipientType == "User" ? targetCustomer?.Name ?? "User" : targetAdmin?.Name ?? "Admin",
            CounterpartyPhoneNumber = recipientType == "User" ? targetCustomer?.PhoneNumber ?? string.Empty : targetAdmin?.PhoneNumber ?? string.Empty,
            IsReadByRecipient = entry.IsReadByRecipient,
            ReadAt = entry.ReadAt,
            CreatedAt = entry.CreatedAt
        });
    }

    [HttpPut("communications/{id:int}/read")]
    public async Task<IActionResult> MarkCommunicationAsRead(int id, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        var entry = await db.CommunicationMessages
            .FirstOrDefaultAsync(
                x => x.Id == id
                     && !x.DeletedByRecipient
                     && (x.RecipientStaffUserId == admin.Id || (x.RecipientType == "Admin" && x.RecipientStaffUserId == null)),
                cancellationToken);

        if (entry is null)
        {
            return NotFound(new { message = "Communication message not found." });
        }

        if (entry.RecipientStaffUserId is null && string.Equals(entry.RecipientType, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            entry.RecipientStaffUserId = admin.Id;
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
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        var entry = await db.CommunicationMessages.FirstOrDefaultAsync(
            x => x.Id == id
                 && (x.SenderStaffUserId == admin.Id
                     || x.RecipientStaffUserId == admin.Id
                     || (x.RecipientType == "Admin" && x.RecipientStaffUserId == null)),
            cancellationToken);

        if (entry is null)
        {
            return NotFound(new { message = "Communication message not found." });
        }

        if (entry.SenderStaffUserId == admin.Id)
        {
            entry.DeletedBySender = true;
        }

        if (entry.RecipientStaffUserId == admin.Id
            || (string.Equals(entry.RecipientType, "Admin", StringComparison.OrdinalIgnoreCase)
                && entry.RecipientStaffUserId is null))
        {
            entry.RecipientStaffUserId ??= admin.Id;
            entry.DeletedByRecipient = true;
        }

        if (entry.DeletedBySender && entry.DeletedByRecipient)
        {
            db.CommunicationMessages.Remove(entry);
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync(
            "Admin",
            "DeleteCommunication",
            "CommunicationMessage",
            id.ToString(),
            "Admin deleted communication message.",
            cancellationToken);
        return NoContent();
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

        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        Customer? targetCustomer = null;
        if (request.CustomerId.HasValue)
        {
            targetCustomer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var normalized = UserOnboardingService.NormalizePhone(request.PhoneNumber);
            targetCustomer = await db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == normalized, cancellationToken);
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject) ? "Admin Issue Escalation" : request.Subject.Trim();
        var targetPhone = !string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? UserOnboardingService.NormalizePhone(request.PhoneNumber)
            : targetCustomer?.PhoneNumber ?? admin.PhoneNumber ?? string.Empty;
        var targetEmail = !string.IsNullOrWhiteSpace(request.Email)
            ? request.Email.Trim().ToLowerInvariant()
            : targetCustomer?.Email ?? admin.Email;

        if (request.SendSms && string.IsNullOrWhiteSpace(targetPhone))
        {
            return BadRequest(new { message = "Target phone number is required for SMS delivery." });
        }

        if (request.SendEmail && string.IsNullOrWhiteSpace(targetEmail))
        {
            return BadRequest(new { message = "Target email is required for email delivery." });
        }

        if (request.SendSms && targetCustomer is not null)
        {
            db.SmsNotifications.Add(new SmsNotification
            {
                CustomerId = targetCustomer.Id,
                SentAt = DateTime.UtcNow,
                IsSuccess = true,
                Message = $"Admin issue SMS: {subject}. {request.Message.Trim()}"
            });
        }

        if (request.SendEmail && targetCustomer is not null)
        {
            db.AccountNotifications.Add(new AccountNotification
            {
                CustomerId = targetCustomer.Id,
                NotificationType = "AdminEmail",
                Title = "Admin Email Queued",
                Message = $"Admin email queued to {targetEmail}. Subject: {subject}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await LogAuditAsync(
            "SupportReport",
            $"Admin support report queued via {(request.SendSms && request.SendEmail ? "SMS+Email" : request.SendSms ? "SMS" : "Email")} to phone '{targetPhone}' and email '{targetEmail}'. Subject: {subject}",
            null,
            cancellationToken);
        await RecordSnapshotAsync(
            "Admin",
            "SupportReport",
            targetCustomer is null ? "StaffUser" : "Customer",
            targetCustomer?.Id.ToString() ?? admin.Id.ToString(),
            $"Issue report queued via {(request.SendSms && request.SendEmail ? "SMS+Email" : request.SendSms ? "SMS" : "Email")}. Subject: {subject}",
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

    [HttpPost("deposits/cash")]
    public async Task<ActionResult<WalletBalanceDto>> PostCashDeposit([FromBody] AdminCashDepositRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var normalizedPhone = UserOnboardingService.NormalizePhone(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return BadRequest(new { message = "Valid phone number is required." });
        }

        var customer = await db.Customers
            .Include(x => x.Account!)
            .ThenInclude(x => x.Wallets)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken);

        if (customer?.Account is null)
        {
            return NotFound(new { message = "Target account was not found." });
        }

        var bankCode = NormalizeBankCode(request.BankCode);
        var reference = BuildReference("ACD", request.Reference);

        customer.Account.Balance += request.Amount;

        var walletTransaction = new WalletTransaction
        {
            CustomerAccountId = customer.Account.Id,
            TransactionType = "AdminCashDeposit",
            Channel = $"Bank-{bankCode}",
            Amount = request.Amount,
            BalanceAfter = customer.Account.Balance,
            CounterpartyPhoneNumber = null,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };
        db.WalletTransactions.Add(walletTransaction);

        db.SmsNotifications.Add(new SmsNotification
        {
            CustomerId = customer.Id,
            SentAt = DateTime.UtcNow,
            IsSuccess = true,
            Message = $"Cash deposit of {request.Amount:0.00} received via {bankCode}. Ref: {reference}."
        });

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(customer.Id, "Deposit Posted", $"Admin posted deposit {request.Amount:0.00}. Ref: {reference}.", "Deposit", walletTransaction.Id, cancellationToken);
        await LogAuditAsync("AdminCashDeposit", $"Posted cash deposit for {normalizedPhone}. Ref: {reference}.", customer.Account.Wallets.FirstOrDefault()?.Id, cancellationToken);
        await RecordSnapshotAsync("Admin", "AdminCashDeposit", "WalletTransaction", walletTransaction.Id.ToString(), $"Posted deposit for {normalizedPhone}", cancellationToken);

        return Ok(new WalletBalanceDto { Balance = customer.Account.Balance });
    }

    [HttpPost("agent-float/adjust")]
    public async Task<ActionResult<WalletBalanceDto>> AdjustAgentFloat([FromBody] AdminAgentFloatAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var normalizedPhone = UserOnboardingService.NormalizePhone(request.PhoneNumber);
        var customer = await db.Customers.Include(x => x.Account).FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken);
        if (customer?.Account is null)
        {
            return NotFound(new { message = "Target account was not found." });
        }

        var normalizedType = request.TransactionType.Trim().ToLowerInvariant();
        var isIn = normalizedType is "in" or "credit";
        if (!isIn && customer.Account.Balance < request.Amount)
        {
            return BadRequest(new { message = "Insufficient funds for float-out." });
        }

        customer.Account.Balance += isIn ? request.Amount : -request.Amount;
        var reference = BuildReference("FLT", request.Reference);

        db.AgentFloatTransactions.Add(new AgentFloatTransaction
        {
            CustomerAccountId = customer.Account.Id,
            Amount = request.Amount,
            TransactionType = isIn ? "In" : "Out",
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        var transaction = new WalletTransaction
        {
            CustomerAccountId = customer.Account.Id,
            TransactionType = isIn ? "AgentFloatIn" : "AgentFloatOut",
            Channel = "AgentFloat",
            Amount = request.Amount,
            BalanceAfter = customer.Account.Balance,
            Reference = reference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };
        db.WalletTransactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(customer.Id, "Agent Float Update", $"Agent float {(isIn ? "credit" : "debit")} {request.Amount:0.00}. Ref: {reference}.", "AgentFloat", transaction.Id, cancellationToken);
        await RecordSnapshotAsync("Admin", "AgentFloatAdjust", "WalletTransaction", transaction.Id.ToString(), $"Agent float {(isIn ? "in" : "out")} for {normalizedPhone}", cancellationToken);
        return Ok(new WalletBalanceDto { Balance = customer.Account.Balance });
    }

    [HttpGet("reversals")]
    public async Task<ActionResult<IReadOnlyList<TransactionReversalDto>>> GetReversals(CancellationToken cancellationToken)
    {
        var items = await db.TransactionReversals.AsNoTracking()
            .OrderByDescending(x => x.RequestedAt)
            .Take(300)
            .Select(ReversalProjection())
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("reversals/request")]
    public async Task<ActionResult<TransactionReversalDto>> CreateReversal([FromBody] CreateTransactionReversalRequest request, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        var transaction = await db.WalletTransactions
            .Include(x => x.CustomerAccount)
            .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == request.WalletTransactionId, cancellationToken);
        if (transaction is null)
        {
            return NotFound(new { message = "Target transaction not found." });
        }

        var pending = await db.TransactionReversals.AnyAsync(x => x.WalletTransactionId == request.WalletTransactionId && x.Status == "Pending", cancellationToken);
        if (pending)
        {
            return BadRequest(new { message = "A pending reversal already exists for this transaction." });
        }

        var reversal = new TransactionReversal
        {
            WalletTransactionId = request.WalletTransactionId,
            RequestedByStaffUserId = admin.Id,
            Reason = request.Reason.Trim(),
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };

        db.TransactionReversals.Add(reversal);
        await db.SaveChangesAsync(cancellationToken);

        await CreateAccountNotificationAsync(transaction.CustomerAccount.CustomerId, "Reversal Requested", $"A reversal was requested for transaction {transaction.Reference}.", "Reversal", transaction.Id, cancellationToken);
        await RecordSnapshotAsync("Admin", "ReversalRequested", "WalletTransaction", transaction.Id.ToString(), $"Reversal requested for {transaction.Reference}", cancellationToken);
        return Ok(new TransactionReversalDto
        {
            Id = reversal.Id,
            WalletTransactionId = reversal.WalletTransactionId,
            Status = reversal.Status,
            Reason = reversal.Reason,
            RequestedAt = reversal.RequestedAt,
            RequestedByStaffUserId = reversal.RequestedByStaffUserId
        });
    }

    [HttpPost("reversals/{id:int}/review")]
    public async Task<ActionResult<TransactionReversalDto>> ReviewReversal(int id, [FromBody] ReviewTransactionReversalRequest request, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        if (!admin.CanApproveReversals)
        {
            return Forbid();
        }

        var reversal = await db.TransactionReversals
            .Include(x => x.WalletTransaction)
            .ThenInclude(x => x.CustomerAccount)
            .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reversal is null)
        {
            return NotFound(new { message = "Reversal request not found." });
        }

        if (!string.Equals(reversal.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Reversal request is already processed." });
        }

        reversal.Status = request.Approve ? "Approved" : "Rejected";
        reversal.ReviewedByStaffUserId = admin.Id;
        reversal.ReviewedAt = DateTime.UtcNow;
        reversal.ReviewNotes = request.Notes?.Trim();

        if (request.Approve)
        {
            var tx = reversal.WalletTransaction;
            var account = tx.CustomerAccount;
            account.Balance += IsOutflowTransaction(tx.TransactionType) ? tx.Amount : -tx.Amount;
            var reversalRef = BuildReference("REV", null);
            reversal.ReversalReference = reversalRef;

            db.WalletTransactions.Add(new WalletTransaction
            {
                CustomerAccountId = account.Id,
                TransactionType = "Reversal",
                Channel = tx.Channel,
                Amount = tx.Amount,
                BalanceAfter = account.Balance,
                CounterpartyPhoneNumber = tx.CounterpartyPhoneNumber,
                Reference = reversalRef,
                Notes = $"Reversal for {tx.Reference}",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await CreateAccountNotificationAsync(reversal.WalletTransaction.CustomerAccount.CustomerId, "Reversal Updated", $"Reversal for {reversal.WalletTransaction.Reference} was {reversal.Status.ToLowerInvariant()}.", "Reversal", reversal.WalletTransactionId, cancellationToken);
        await RecordSnapshotAsync("Admin", "ReversalReviewed", "TransactionReversal", reversal.Id.ToString(), $"Reversal {reversal.Status.ToLowerInvariant()} for {reversal.WalletTransaction.Reference}", cancellationToken);
        return Ok(await db.TransactionReversals.AsNoTracking().Where(x => x.Id == reversal.Id).Select(ReversalProjection()).FirstAsync(cancellationToken));
    }

    [HttpGet("snapshots")]
    public async Task<ActionResult<IReadOnlyList<ActivitySnapshotDto>>> GetSnapshots([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var items = await db.ActivitySnapshots.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .Select(x => new ActivitySnapshotDto
            {
                Id = x.Id,
                ActorType = x.ActorType,
                ActorId = x.ActorId,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("settings/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAdminSettingsRequest request, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        admin.Name = request.Name.Trim();
        admin.Email = request.Email.Trim().ToLowerInvariant();
        admin.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        admin.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : UserOnboardingService.NormalizePhone(request.PhoneNumber);
        admin.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        admin.NotificationsEnabled = request.NotificationsEnabled;
        admin.CanApproveReversals = request.CanApproveReversals;
        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync("Admin", "UpdateAdminProfile", "StaffUser", admin.Id.ToString(), "Admin updated profile settings.", cancellationToken);

        return Ok(new
        {
            admin.Id,
            admin.Username,
            admin.Name,
            admin.Email,
            admin.Role,
            admin.PhoneNumber,
            admin.Department,
            admin.NotificationsEnabled,
            admin.CanApproveReversals,
            admin.ProfileImageUrl
        });
    }

    [HttpPut("settings/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "New password must be at least 6 characters long." });
        }

        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized(new { message = "Invalid admin context." });
        }

        if (string.IsNullOrWhiteSpace(admin.PasswordHash)
            || !admin.PasswordHash.StartsWith("$2")
            || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, admin.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword.Trim());
        await db.SaveChangesAsync(cancellationToken);
        await RecordSnapshotAsync("Admin", "ChangeAdminPassword", "StaffUser", admin.Id.ToString(), "Admin changed password.", cancellationToken);
        return Ok(new { message = "Password updated." });
    }

    private async Task<StaffUser?> GetAdminAsync(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue("staff_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var adminId))
        {
            return null;
        }

        return await db.StaffUsers.FirstOrDefaultAsync(x => x.Id == adminId, cancellationToken);
    }

    private async Task LogAuditAsync(string action, string details, int? walletId, CancellationToken cancellationToken)
    {
        var admin = await GetAdminAsync(cancellationToken);
        if (admin is null)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            StaffUserId = admin.Id,
            WalletId = walletId,
            Action = action,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AdminReportFiltersDto BuildFilters(
        string? range,
        int? userId,
        string? phoneNumber,
        string? name,
        string? transactionType,
        string? channel,
        DateTime? startUtc,
        DateTime? endUtc) =>
        new()
        {
            Range = range ?? "30d",
            CustomerId = userId,
            PhoneNumber = phoneNumber,
            Name = name,
            TransactionType = transactionType,
            Channel = channel,
            StartUtc = startUtc,
            EndUtc = endUtc
        };

    private async Task CreateAccountNotificationAsync(
        int customerId,
        string title,
        string message,
        string notificationType,
        int? walletTransactionId,
        CancellationToken cancellationToken)
    {
        db.AccountNotifications.Add(new AccountNotification
        {
            CustomerId = customerId,
            WalletTransactionId = walletTransactionId,
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
        string action,
        string entityType,
        string? entityId,
        string details,
        CancellationToken cancellationToken)
    {
        int? actorId = null;
        if (string.Equals(actorType, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            actorId = (await GetAdminAsync(cancellationToken))?.Id;
        }

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

    private async Task<AdminDashboardDto> BuildAdminDashboardAsync(AdminReportFiltersDto filters, CancellationToken cancellationToken)
    {
        var range = ResolveDateRange(filters.Range, filters.StartUtc, filters.EndUtc, "30d");
        filters.Range = range.RangeKey;
        filters.StartUtc = range.RangeStartUtc;
        filters.EndUtc = range.RangeEndUtc;

        var users = await db.Customers
            .AsNoTracking()
            .Include(x => x.Account!)
            .ToListAsync(cancellationToken);

        var accounts = users
            .Where(x => x.Account is not null)
            .Select(x => new AdminAccountAnalyticsDto
            {
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                AccountNumber = x.Account!.AccountNumber,
                Balance = x.Account.Balance,
                IsFrozen = x.Account.IsFrozen
            })
            .OrderByDescending(x => x.Balance)
            .ToList();

        var transactions = await BuildFilteredTransactionsQuery(filters)
            .OrderByDescending(x => x.CreatedAt)
            .Select(TransactionProjection())
            .ToListAsync(cancellationToken);

        var snapshots = await db.ActivitySnapshots.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(80)
            .Select(x => new ActivitySnapshotDto
            {
                Id = x.Id,
                ActorType = x.ActorType,
                ActorId = x.ActorId,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var inflow = transactions.Where(x => !IsOutflowTransaction(x.TransactionType)).Sum(x => x.Amount);
        var outflow = transactions.Where(x => IsOutflowTransaction(x.TransactionType)).Sum(x => x.Amount);

        return new AdminDashboardDto
        {
            DateRangeKey = range.RangeKey,
            RangeStartUtc = range.RangeStartUtc,
            RangeEndUtc = range.RangeEndUtc,
            TotalUsers = users.Count,
            ActiveUsers = users.Count(x => x.IsActive),
            TotalBalanceAcrossAccounts = accounts.Sum(x => x.Balance),
            TotalTransactions = transactions.Count,
            TotalTransactionsAllTime = await db.WalletTransactions.CountAsync(cancellationToken),
            TotalInflow = inflow,
            TotalOutflow = outflow,
            NetMovement = inflow - outflow,
            Trend = BuildTrend(transactions, range.RangeStartUtc, range.RangeEndUtc, range.TrendDays),
            TransactionBreakdown = BuildTransactionBreakdown(transactions),
            UserSnapshots = BuildUserSnapshots(users, transactions),
            RecentSnapshots = snapshots,
            Accounts = accounts,
            Transactions = transactions
        };
    }

    private IQueryable<WalletTransaction> BuildFilteredTransactionsQuery(AdminReportFiltersDto filters)
    {
        var range = ResolveDateRange(filters.Range, filters.StartUtc, filters.EndUtc, "all");
        var query = db.WalletTransactions.AsNoTracking()
            .Include(x => x.CustomerAccount)
            .ThenInclude(x => x.Customer)
            .Where(x => x.CreatedAt <= range.RangeEndUtc);

        if (range.RangeStartUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= range.RangeStartUtc.Value);
        }

        if (filters.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerAccount.CustomerId == filters.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.PhoneNumber))
        {
            var normalizedPhone = UserOnboardingService.NormalizePhone(filters.PhoneNumber);
            query = query.Where(x => x.CustomerAccount.Customer.PhoneNumber == normalizedPhone || x.CounterpartyPhoneNumber == normalizedPhone);
        }

        if (!string.IsNullOrWhiteSpace(filters.Name))
        {
            var normalizedName = filters.Name.Trim().ToLowerInvariant();
            query = query.Where(x => x.CustomerAccount.Customer.Name.ToLower().Contains(normalizedName));
        }

        if (!string.IsNullOrWhiteSpace(filters.TransactionType))
        {
            var selectedType = filters.TransactionType.Trim().ToLowerInvariant();
            query = query.Where(x => x.TransactionType.ToLower() == selectedType);
        }

        if (!string.IsNullOrWhiteSpace(filters.Channel))
        {
            var selectedChannel = filters.Channel.Trim().ToLowerInvariant();
            query = query.Where(x => x.Channel.ToLower().Contains(selectedChannel));
        }

        return query;
    }

    private static Expression<Func<WalletTransaction, WalletTransactionDto>> TransactionProjection() =>
        x => new WalletTransactionDto
        {
            Id = x.Id,
            CustomerAccountId = x.CustomerAccountId,
            CustomerId = x.CustomerAccount.CustomerId,
            CustomerName = x.CustomerAccount.Customer.Name,
            CustomerPhoneNumber = x.CustomerAccount.Customer.PhoneNumber,
            TransactionType = x.TransactionType,
            Channel = x.Channel,
            Amount = x.Amount,
            BalanceAfter = x.BalanceAfter,
            CounterpartyPhoneNumber = x.CounterpartyPhoneNumber,
            Reference = x.Reference,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt
        };

    private static Expression<Func<TransactionReversal, TransactionReversalDto>> ReversalProjection() =>
        x => new TransactionReversalDto
        {
            Id = x.Id,
            WalletTransactionId = x.WalletTransactionId,
            Status = x.Status,
            Reason = x.Reason,
            RequestedAt = x.RequestedAt,
            RequestedByStaffUserId = x.RequestedByStaffUserId,
            ReviewedByStaffUserId = x.ReviewedByStaffUserId,
            ReviewedAt = x.ReviewedAt,
            ReviewNotes = x.ReviewNotes,
            ReversalReference = x.ReversalReference
        };

    private static AdminDashboardDto FilterDashboardByDataset(AdminDashboardDto dashboard, string dataset) =>
        dataset switch
        {
            "summary" => CopyDashboard(dashboard, includeAccounts: false, includeTransactions: false, includeSnapshots: false),
            "transactions" => CopyDashboard(dashboard, includeAccounts: false, includeTransactions: true, includeSnapshots: false),
            "users" => CopyDashboard(dashboard, includeAccounts: true, includeTransactions: false, includeSnapshots: false),
            "snapshots" => CopyDashboard(dashboard, includeAccounts: false, includeTransactions: false, includeSnapshots: true),
            _ => dashboard
        };

    private static AdminDashboardDto CopyDashboard(AdminDashboardDto source, bool includeAccounts, bool includeTransactions, bool includeSnapshots) =>
        new()
        {
            DateRangeKey = source.DateRangeKey,
            RangeStartUtc = source.RangeStartUtc,
            RangeEndUtc = source.RangeEndUtc,
            TotalUsers = source.TotalUsers,
            ActiveUsers = source.ActiveUsers,
            TotalBalanceAcrossAccounts = source.TotalBalanceAcrossAccounts,
            TotalTransactions = source.TotalTransactions,
            TotalTransactionsAllTime = source.TotalTransactionsAllTime,
            TotalInflow = source.TotalInflow,
            TotalOutflow = source.TotalOutflow,
            NetMovement = source.NetMovement,
            Trend = source.Trend,
            TransactionBreakdown = source.TransactionBreakdown,
            UserSnapshots = includeSnapshots ? source.UserSnapshots : [],
            RecentSnapshots = includeSnapshots ? source.RecentSnapshots : [],
            Accounts = includeAccounts ? source.Accounts : [],
            Transactions = includeTransactions ? source.Transactions : []
        };

    private static string NormalizeDataset(string? dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset))
        {
            return "summary";
        }

        return dataset.Trim().ToLowerInvariant() switch
        {
            "summary" => "summary",
            "transactions" => "transactions",
            "users" => "users",
            "snapshots" => "snapshots",
            "all" => "all",
            _ => "summary"
        };
    }

    private static (string RangeKey, DateTime? RangeStartUtc, DateTime RangeEndUtc, int TrendDays) ResolveDateRange(
        string? range,
        DateTime? startUtc,
        DateTime? endUtc,
        string defaultRangeKey)
    {
        var rangeKey = NormalizeRangeKey(range, defaultRangeKey);
        var rangeEndUtc = endUtc ?? DateTime.UtcNow;
        if (startUtc.HasValue && endUtc.HasValue)
        {
            var days = Math.Max(1, (int)Math.Ceiling((endUtc.Value.Date - startUtc.Value.Date).TotalDays) + 1);
            return (rangeKey, startUtc.Value.Date, rangeEndUtc, Math.Min(days, 120));
        }

        var todayUtc = DateTime.UtcNow.Date;
        return rangeKey switch
        {
            "7d" => (rangeKey, todayUtc.AddDays(-6), rangeEndUtc, 7),
            "30d" => (rangeKey, todayUtc.AddDays(-29), rangeEndUtc, 30),
            "90d" => (rangeKey, todayUtc.AddDays(-89), rangeEndUtc, 90),
            _ => (rangeKey, null, rangeEndUtc, 30)
        };
    }

    private static string NormalizeRangeKey(string? range, string defaultRangeKey)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return defaultRangeKey;
        }

        return range.Trim().ToLowerInvariant() switch
        {
            "7d" or "last7d" or "last7days" => "7d",
            "30d" or "last30d" or "last30days" => "30d",
            "90d" or "last90d" or "last90days" => "90d",
            "all" or "alltime" => "all",
            _ => defaultRangeKey
        };
    }

    private static IReadOnlyList<AdminTrendPointDto> BuildTrend(
        IReadOnlyList<WalletTransactionDto> transactions,
        DateTime? rangeStartUtc,
        DateTime rangeEndUtc,
        int trendDays)
    {
        var grouped = transactions
            .GroupBy(x => x.CreatedAt.ToString("yyyy-MM-dd"))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var inflow = x.Where(t => !IsOutflowTransaction(t.TransactionType)).Sum(t => t.Amount);
                    var outflow = x.Where(t => IsOutflowTransaction(t.TransactionType)).Sum(t => t.Amount);
                    return new
                    {
                        Count = x.Count(),
                        Volume = x.Sum(t => t.Amount),
                        Inflow = inflow,
                        Outflow = outflow
                    };
                });

        var endDate = rangeEndUtc.Date;
        var startDate = rangeStartUtc?.Date ?? endDate.AddDays(-(trendDays - 1));
        if ((endDate - startDate).TotalDays + 1 > trendDays)
        {
            startDate = endDate.AddDays(-(trendDays - 1));
        }

        var trend = new List<AdminTrendPointDto>();
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            var dateKey = day.ToString("yyyy-MM-dd");
            grouped.TryGetValue(dateKey, out var existing);
            trend.Add(new AdminTrendPointDto
            {
                DateKey = dateKey,
                Label = day.ToString("MMM d"),
                TransactionCount = existing?.Count ?? 0,
                TransactionVolume = existing?.Volume ?? 0m,
                Inflow = existing?.Inflow ?? 0m,
                Outflow = existing?.Outflow ?? 0m
            });
        }

        return trend;
    }

    private static IReadOnlyList<AdminTransactionBreakdownDto> BuildTransactionBreakdown(IReadOnlyList<WalletTransactionDto> transactions)
    {
        var totalVolume = transactions.Sum(x => x.Amount);
        if (totalVolume <= 0)
        {
            return [];
        }

        return transactions
            .GroupBy(x => string.IsNullOrWhiteSpace(x.TransactionType) ? "Unknown" : x.TransactionType.Trim())
            .Select(x =>
            {
                var volume = x.Sum(v => v.Amount);
                return new AdminTransactionBreakdownDto
                {
                    Label = x.Key,
                    TransactionCount = x.Count(),
                    TotalAmount = volume,
                    PercentageOfVolume = decimal.Round((volume / totalVolume) * 100m, 2)
                };
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();
    }

    private static IReadOnlyList<AdminUserSnapshotDto> BuildUserSnapshots(IReadOnlyList<Customer> users, IReadOnlyList<WalletTransactionDto> transactions)
    {
        var metricsByAccount = transactions
            .GroupBy(x => x.CustomerAccountId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var inflow = x.Where(t => !IsOutflowTransaction(t.TransactionType)).Sum(t => t.Amount);
                    var outflow = x.Where(t => IsOutflowTransaction(t.TransactionType)).Sum(t => t.Amount);
                    return new
                    {
                        Count = x.Count(),
                        Inflow = inflow,
                        Outflow = outflow,
                        LastAt = x.Max(v => (DateTime?)v.CreatedAt)
                    };
                });

        return users
            .Where(x => x.Account is not null)
            .Select(x =>
            {
                metricsByAccount.TryGetValue(x.Account!.Id, out var metrics);
                var inflow = metrics?.Inflow ?? 0m;
                var outflow = metrics?.Outflow ?? 0m;

                return new AdminUserSnapshotDto
                {
                    CustomerId = x.Id,
                    Name = x.Name,
                    PhoneNumber = x.PhoneNumber,
                    AccountNumber = x.Account!.AccountNumber,
                    Balance = x.Account.Balance,
                    IsFrozen = x.Account.IsFrozen,
                    IsActive = x.IsActive,
                    TransactionCount = metrics?.Count ?? 0,
                    Inflow = inflow,
                    Outflow = outflow,
                    NetMovement = inflow - outflow,
                    LastTransactionAt = metrics?.LastAt
                };
            })
            .OrderByDescending(x => x.Balance)
            .ThenByDescending(x => x.TransactionCount)
            .ToList();
    }

    private async Task<string> GenerateUniqueAccountNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        var suffix = digits.Length <= 8 ? digits : digits[^8..];

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"TDW{suffix}{Random.Shared.Next(100, 999)}";
            var exists = await db.CustomerAccounts.AnyAsync(x => x.AccountNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"TDW{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private async Task<string> GenerateUniqueNfcCardUidAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        var suffix = digits.Length <= 6 ? digits : digits[^6..];

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"NFC-{suffix}-{Guid.NewGuid():N}"[..24];
            var exists = await db.NfcCards.AnyAsync(x => x.CardUid == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"NFC-{Guid.NewGuid():N}"[..24];
    }

    private static string BuildReference(string prefix, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested.Trim();
        }

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }

    private static string NormalizeBankCode(string bankCode)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
        {
            return "AnyBank";
        }

        var normalized = new string(bankCode.Trim().Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(normalized)
            ? "AnyBank"
            : normalized[..Math.Min(normalized.Length, 30)];
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

    private static bool IsOutflowTransaction(string transactionType)
    {
        var normalized = transactionType.Trim().ToLowerInvariant();
        return normalized is "withdrawal" or "transferout" or "merchantpayment" or "billpayment" or "airtimepurchase" or "agentfloatout";
    }

    private static AdminUserDto MapUser(Customer customer)
    {
        var account = customer.Account;
        var wallet = account?.Wallets.FirstOrDefault();
        var nfc = wallet?.NfcCards.FirstOrDefault();

        return new AdminUserDto
        {
            CustomerId = customer.Id,
            Name = customer.Name,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email,
            AccountNumber = account?.AccountNumber ?? string.Empty,
            Balance = account?.Balance ?? 0m,
            IsFrozen = account?.IsFrozen ?? false,
            IsActive = customer.IsActive,
            NfcCardUid = nfc?.CardUid ?? string.Empty,
            NotificationsEnabled = customer.NotificationsEnabled,
            PreferredLanguage = customer.PreferredLanguage,
            DailyTransferLimit = customer.DailyTransferLimit
        };
    }

    private static AdminUserDto MapUserFromOnboarded(OnboardedUserResult onboarded) =>
        new()
        {
            CustomerId = onboarded.Customer.Id,
            Name = onboarded.Customer.Name,
            PhoneNumber = onboarded.Customer.PhoneNumber,
            Email = onboarded.Customer.Email,
            AccountNumber = onboarded.Account.AccountNumber,
            Balance = onboarded.Account.Balance,
            IsFrozen = onboarded.Account.IsFrozen,
            IsActive = onboarded.Customer.IsActive,
            NfcCardUid = onboarded.NfcCard.CardUid,
            NotificationsEnabled = onboarded.Customer.NotificationsEnabled,
            PreferredLanguage = onboarded.Customer.PreferredLanguage,
            DailyTransferLimit = onboarded.Customer.DailyTransferLimit
        };
}


