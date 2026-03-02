namespace SMS.Data.EntityModels;

public class StaffUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool CanApproveReversals { get; set; }
    public bool IsActive { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<TransactionReversal> RequestedReversals { get; set; } = new List<TransactionReversal>();
    public ICollection<TransactionReversal> ReviewedReversals { get; set; } = new List<TransactionReversal>();
    public ICollection<CommunicationMessage> SentCommunicationMessages { get; set; } = new List<CommunicationMessage>();
    public ICollection<CommunicationMessage> ReceivedCommunicationMessages { get; set; } = new List<CommunicationMessage>();
}


