namespace SMS.Core.Dtos;

public class RegisterUserRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class UserLoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginRequest
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthIdentityDto
{
    public int? CustomerId { get; set; }
    public int? StaffUserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AuthIdentityDto Identity { get; set; } = new();
}

public class WalletTransactionDto
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhoneNumber { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? CounterpartyPhoneNumber { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserProfileDto
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ProfileImageUrl { get; set; }
}

public class WalletAccessDto
{
    public int WalletId { get; set; }
    public int CustomerAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }
    public string NfcCardUid { get; set; } = string.Empty;
    public string NfcLinkedPhoneNumber { get; set; } = string.Empty;
}

public class UserQrCodeDto
{
    public string Provider { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public decimal Amount { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

public class UserDashboardDto
{
    public UserProfileDto Profile { get; set; } = new();
    public WalletAccessDto Wallet { get; set; } = new();
    public IReadOnlyList<WalletTransactionDto> Transactions { get; set; } = [];
    public IReadOnlyList<SmsNotificationDto> SmsNotifications { get; set; } = [];
    public IReadOnlyList<AccountNotificationDto> Notifications { get; set; } = [];
    public int UnreadNotifications { get; set; }
}

public class AccountNotificationDto
{
    public int Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CommunicationMessageDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public int? SenderCustomerId { get; set; }
    public int? SenderStaffUserId { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public int? RecipientCustomerId { get; set; }
    public int? RecipientStaffUserId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string CounterpartyPhoneNumber { get; set; } = string.Empty;
    public bool IsReadByRecipient { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCommunicationMessageRequest
{
    public string RecipientType { get; set; } = "Admin";
    public int? RecipientCustomerId { get; set; }
    public int? RecipientStaffUserId { get; set; }
    public string? RecipientPhoneNumber { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ActivitySnapshotDto
{
    public int Id { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public int? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WalletOperationRequest
{
    public decimal Amount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class TransferRequest
{
    public string TargetPhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class GenerateQrRequest
{
    public string Provider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class UpdateUserSettingsRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public decimal DailyTransferLimit { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class WalletBalanceDto
{
    public decimal Balance { get; set; }
}

public class AdminUserDto
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsActive { get; set; }
    public string NfcCardUid { get; set; } = string.Empty;
    public bool NotificationsEnabled { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public decimal DailyTransferLimit { get; set; }
}

public class AdminCreateUserRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public bool IsFrozen { get; set; }
}

public class AdminUpdateUserRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsFrozen { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public decimal DailyTransferLimit { get; set; }
}

public class AdminIssueNfcCardRequest
{
    public string? CardUid { get; set; }
    public bool ReplaceExisting { get; set; } = true;
    public bool EnsureAccessMethod { get; set; } = true;
    public bool EnsureQrToken { get; set; } = true;
    public int QrTokenExpiryDays { get; set; } = 30;
    public int QrTokenMaxUsage { get; set; } = 500;
}

public class AdminIssueNfcCardResultDto
{
    public int CustomerId { get; set; }
    public int WalletId { get; set; }
    public string CardUid { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool ReplacedExistingCard { get; set; }
    public bool AccessMethodCreated { get; set; }
    public bool QrTokenCreated { get; set; }
    public string? QrToken { get; set; }
    public DateTime? QrTokenExpiry { get; set; }
}

public class AdminCashDepositRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class AdminAccountAnalyticsDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }
}

public class AdminDashboardDto
{
    public string DateRangeKey { get; set; } = "30d";
    public DateTime? RangeStartUtc { get; set; }
    public DateTime RangeEndUtc { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public decimal TotalBalanceAcrossAccounts { get; set; }
    public int TotalTransactions { get; set; }
    public int TotalTransactionsAllTime { get; set; }
    public decimal TotalInflow { get; set; }
    public decimal TotalOutflow { get; set; }
    public decimal NetMovement { get; set; }
    public IReadOnlyList<AdminTrendPointDto> Trend { get; set; } = [];
    public IReadOnlyList<AdminTransactionBreakdownDto> TransactionBreakdown { get; set; } = [];
    public IReadOnlyList<AdminUserSnapshotDto> UserSnapshots { get; set; } = [];
    public IReadOnlyList<ActivitySnapshotDto> RecentSnapshots { get; set; } = [];
    public IReadOnlyList<AdminAccountAnalyticsDto> Accounts { get; set; } = [];
    public IReadOnlyList<WalletTransactionDto> Transactions { get; set; } = [];
}

public class AdminTrendPointDto
{
    public string DateKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TransactionVolume { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
}

public class AdminTransactionBreakdownDto
{
    public string Label { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PercentageOfVolume { get; set; }
}

public class AdminUserSnapshotDto
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsActive { get; set; }
    public int TransactionCount { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal NetMovement { get; set; }
    public DateTime? LastTransactionAt { get; set; }
}

public class AdminExportPayloadDto
{
    public string Dataset { get; set; } = "summary";
    public AdminReportFiltersDto Filters { get; set; } = new();
    public AdminDashboardDto Dashboard { get; set; } = new();
}

public class AdminWorkspaceDto
{
    public int PendingReversals { get; set; }
    public int UnreadUserMessages { get; set; }
    public int FrozenAccounts { get; set; }
    public int DormantWallets { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<ActivitySnapshotDto> RecentIncidents { get; set; } = [];
}

public class AdminBroadcastRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetSegment { get; set; } = "AllUsers";
    public bool SendSms { get; set; } = false;
    public decimal LowBalanceThreshold { get; set; } = 10m;
}

public class AdminBroadcastResultDto
{
    public string TargetSegment { get; set; } = "AllUsers";
    public int Recipients { get; set; }
    public bool SmsQueued { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class AdminReportFiltersDto
{
    public string Range { get; set; } = "30d";
    public int? CustomerId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Name { get; set; }
    public string? TransactionType { get; set; }
    public string? Channel { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
}

public class AdminReportPreviewDto
{
    public AdminReportFiltersDto Filters { get; set; } = new();
    public int ResultCount { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<WalletTransactionDto> Transactions { get; set; } = [];
}

public class SendTransactionTraceRequest
{
    public int? CustomerId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Name { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AdminAgentFloatAdjustmentRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = "In";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class CreateTransactionReversalRequest
{
    public int WalletTransactionId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReviewTransactionReversalRequest
{
    public bool Approve { get; set; }
    public string? Notes { get; set; }
}

public class TransactionReversalDto
{
    public int Id { get; set; }
    public int WalletTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public int RequestedByStaffUserId { get; set; }
    public int? ReviewedByStaffUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReversalReference { get; set; }
}

public class MerchantTillPaymentRequest
{
    public string MerchantName { get; set; } = string.Empty;
    public string TillNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class BillPaymentRequest
{
    public string BillerCode { get; set; } = string.Empty;
    public string BillerName { get; set; } = string.Empty;
    public string AccountReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class AirtimePurchaseRequest
{
    public string Network { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class AirtimePurchaseResultDto
{
    public decimal WalletBalance { get; set; }
    public decimal AirtimeBalanceAfter { get; set; }
    public string VoucherCode { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class IssueCommunicationRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool SendSms { get; set; } = true;
    public bool SendEmail { get; set; } = true;
    public int? CustomerId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class IssueCommunicationResultDto
{
    public bool SmsQueued { get; set; }
    public bool EmailQueued { get; set; }
    public string TargetPhoneNumber { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class UserQuickActionItemDto
{
    public string ActionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? DisabledReason { get; set; }
}

public class UserExperienceFeedDto
{
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
    public IReadOnlyList<AccountNotificationDto> Notifications { get; set; } = [];
    public IReadOnlyList<CommunicationMessageDto> Messages { get; set; } = [];
}

public class UserFeedbackRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Urgent { get; set; } = false;
}

public class UserFeedbackResultDto
{
    public int CommunicationId { get; set; }
    public string Status { get; set; } = "Submitted";
    public DateTime SubmittedAtUtc { get; set; }
}

public class NfcDepositRequest
{
    public string CardUid { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class QrDepositRequest
{
    public string Token { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UserTransactionExportDto
{
    public string TransactionType { get; set; } = "all";
    public IReadOnlyList<WalletTransactionDto> Transactions { get; set; } = [];
}

public class UpdateAdminSettingsRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool CanApproveReversals { get; set; }
}


