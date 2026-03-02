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
[Route("api/user/experience")]
[Authorize(Roles = "User")]
public class UserExperienceController(SmsDbContext db) : ControllerBase
{
    [HttpGet("quick-actions")]
    public async Task<ActionResult<IReadOnlyList<UserQuickActionItemDto>>> GetQuickActions(CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var isFrozen = context.Account.IsFrozen;
        var canSpend = !isFrozen && context.Account.Balance > 0;
        var actions = new List<UserQuickActionItemDto>
        {
            new()
            {
                ActionKey = "deposit",
                Title = "Deposit Funds",
                Description = "Top up your wallet through available channels.",
                Enabled = !isFrozen,
                DisabledReason = isFrozen ? "Account is frozen." : null
            },
            new()
            {
                ActionKey = "pay",
                Title = "Make Payment",
                Description = "Pay merchants and bills from your wallet.",
                Enabled = canSpend,
                DisabledReason = canSpend ? null : isFrozen ? "Account is frozen." : "Insufficient balance."
            },
            new()
            {
                ActionKey = "transfer",
                Title = "Transfer Funds",
                Description = "Send money to another wallet holder.",
                Enabled = canSpend,
                DisabledReason = canSpend ? null : isFrozen ? "Account is frozen." : "Insufficient balance."
            },
            new()
            {
                ActionKey = "support",
                Title = "Contact Support",
                Description = "Raise a support issue directly to admin.",
                Enabled = true
            }
        };

        return Ok(actions);
    }

    [HttpGet("feed")]
    public async Task<ActionResult<UserExperienceFeedDto>> GetFeed(CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var notifications = await db.AccountNotifications.AsNoTracking()
            .Where(x => x.CustomerId == context.Customer.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
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

        var messages = await db.CommunicationMessages.AsNoTracking()
            .Where(x => x.RecipientCustomerId == context.Customer.Id && !x.DeletedByRecipient)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
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
                Direction = "Received",
                CounterpartyName = x.SenderStaffUser != null ? x.SenderStaffUser.Name : "Support",
                CounterpartyPhoneNumber = x.SenderStaffUser != null ? x.SenderStaffUser.PhoneNumber ?? string.Empty : string.Empty,
                IsReadByRecipient = x.IsReadByRecipient,
                ReadAt = x.ReadAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new UserExperienceFeedDto
        {
            UnreadNotifications = notifications.Count(x => !x.IsRead),
            UnreadMessages = messages.Count(x => !x.IsReadByRecipient),
            Notifications = notifications,
            Messages = messages
        });
    }

    [HttpPost("feedback")]
    public async Task<ActionResult<UserFeedbackResultDto>> SubmitFeedback([FromBody] UserFeedbackRequest request, CancellationToken cancellationToken)
    {
        var context = await GetUserContextAsync(cancellationToken);
        if (context is null)
        {
            return Unauthorized(new { message = "Invalid user context." });
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Feedback message is required." });
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "User Feedback"
            : request.Subject.Trim();

        if (request.Urgent && !subject.StartsWith("[URGENT]", StringComparison.OrdinalIgnoreCase))
        {
            subject = $"[URGENT] {subject}";
        }

        var targetAdmin = await db.StaffUsers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetAdmin is null)
        {
            return BadRequest(new { message = "No active admin is available." });
        }

        var communication = new CommunicationMessage
        {
            Subject = subject,
            Message = message,
            SenderType = "User",
            SenderCustomerId = context.Customer.Id,
            RecipientType = "Admin",
            RecipientStaffUserId = targetAdmin.Id,
            IsReadByRecipient = false,
            CreatedAt = DateTime.UtcNow
        };

        db.CommunicationMessages.Add(communication);
        db.AccountNotifications.Add(new AccountNotification
        {
            CustomerId = context.Customer.Id,
            NotificationType = "Support",
            Title = "Feedback Submitted",
            Message = "Your message has been sent to support.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        db.ActivitySnapshots.Add(new ActivitySnapshot
        {
            ActorType = "User",
            ActorId = context.Customer.Id,
            Action = "SubmitFeedback",
            EntityType = "CommunicationMessage",
            EntityId = context.Customer.Id.ToString(),
            Details = $"Feedback sent to admin {targetAdmin.Id}. Urgent={request.Urgent}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new UserFeedbackResultDto
        {
            CommunicationId = communication.Id,
            Status = "Submitted",
            SubmittedAtUtc = communication.CreatedAt
        });
    }

    private async Task<UserContext?> GetUserContextAsync(CancellationToken cancellationToken)
    {
        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(customerIdClaim, out var customerId))
        {
            return null;
        }

        var customer = await db.Customers
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer?.Account is null)
        {
            return null;
        }

        return new UserContext(customer, customer.Account);
    }

    private sealed record UserContext(Customer Customer, CustomerAccount Account);
}


