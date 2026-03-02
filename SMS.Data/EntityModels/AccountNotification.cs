namespace SMS.Data.EntityModels;

public class AccountNotification
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? WalletTransactionId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public WalletTransaction? WalletTransaction { get; set; }
}


