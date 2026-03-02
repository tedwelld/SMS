using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/admin/experience")]
[Authorize(Roles = "Admin,Staff")]
public class AdminExperienceController(SmsDbContext db) : ControllerBase
{
    [HttpGet("workspace")]
    public async Task<ActionResult<AdminWorkspaceDto>> GetWorkspace(CancellationToken cancellationToken)
    {
        var pendingReversals = await db.TransactionReversals.AsNoTracking()
            .CountAsync(x => x.Status == "Pending", cancellationToken);

        var unreadUserMessages = await db.CommunicationMessages.AsNoTracking()
            .CountAsync(
                x => x.RecipientType == "Admin"
                    && !x.IsReadByRecipient
                    && !x.DeletedByRecipient,
                cancellationToken);

        var frozenAccounts = await db.CustomerAccounts.AsNoTracking()
            .CountAsync(x => x.IsFrozen, cancellationToken);

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var dormantWallets = await db.Wallets.AsNoTracking()
            .CountAsync(
                x => !db.WalletTransactions.Any(t => t.CustomerAccountId == x.CustomerAccountId && t.CreatedAt >= cutoff),
                cancellationToken);

        var recentIncidents = await db.ActivitySnapshots.AsNoTracking()
            .Where(x =>
                EF.Functions.Like(x.Action, "%Fail%")
                || EF.Functions.Like(x.Action, "%Reversal%")
                || EF.Functions.Like(x.Action, "%Support%")
                || EF.Functions.Like(x.Action, "%Fraud%"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
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

        return Ok(new AdminWorkspaceDto
        {
            PendingReversals = pendingReversals,
            UnreadUserMessages = unreadUserMessages,
            FrozenAccounts = frozenAccounts,
            DormantWallets = dormantWallets,
            GeneratedAtUtc = DateTime.UtcNow,
            RecentIncidents = recentIncidents
        });
    }

    [HttpPost("broadcast")]
    public async Task<ActionResult<AdminBroadcastResultDto>> Broadcast(
        [FromBody] AdminBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Title and message are required." });
        }

        var segment = NormalizeSegment(request.TargetSegment);
        var lowBalanceThreshold = request.LowBalanceThreshold <= 0 ? 10m : request.LowBalanceThreshold;

        var customersQuery = db.Customers
            .Include(x => x.Account)
            .Where(x => x.IsActive)
            .AsQueryable();

        customersQuery = segment switch
        {
            "FrozenAccounts" => customersQuery.Where(x => x.Account != null && x.Account.IsFrozen),
            "LowBalanceUsers" => customersQuery.Where(x => x.Account != null && x.Account.Balance <= lowBalanceThreshold),
            "InactiveUsers" => db.Customers.Include(x => x.Account).Where(x => !x.IsActive),
            _ => customersQuery
        };

        var recipients = await customersQuery.ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return BadRequest(new { message = "No matching users for selected segment." });
        }

        foreach (var customer in recipients)
        {
            db.AccountNotifications.Add(new AccountNotification
            {
                CustomerId = customer.Id,
                NotificationType = "AdminBroadcast",
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            if (request.SendSms)
            {
                db.SmsNotifications.Add(new SmsNotification
                {
                    CustomerId = customer.Id,
                    Message = $"[Admin Notice] {title}: {message}",
                    SentAt = DateTime.UtcNow,
                    IsSuccess = true
                });
            }
        }

        db.ActivitySnapshots.Add(new ActivitySnapshot
        {
            ActorType = "Admin",
            ActorId = GetStaffId(),
            Action = "BroadcastNotice",
            EntityType = "Customer",
            EntityId = string.Join(',', recipients.Select(x => x.Id)),
            Details = $"Segment {segment}, recipients {recipients.Count}, sms {request.SendSms}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new AdminBroadcastResultDto
        {
            TargetSegment = segment,
            Recipients = recipients.Count,
            SmsQueued = request.SendSms,
            GeneratedAtUtc = DateTime.UtcNow
        });
    }

    private int? GetStaffId()
    {
        var claim = User.FindFirstValue("staff_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var id))
        {
            return id;
        }

        return null;
    }

    private static string NormalizeSegment(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "AllUsers";
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "allusers" => "AllUsers",
            "frozenaccounts" => "FrozenAccounts",
            "lowbalanceusers" => "LowBalanceUsers",
            "inactiveusers" => "InactiveUsers",
            _ => "AllUsers"
        };
    }
}


