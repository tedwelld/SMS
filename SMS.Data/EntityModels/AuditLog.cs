namespace SMS.Data.EntityModels;

public class AuditLog
{
    public int Id { get; set; }
    public int StaffUserId { get; set; }
    public int? WalletId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } = string.Empty;

    public StaffUser StaffUser { get; set; } = null!;
    public Wallet? Wallet { get; set; }
}


