namespace SMS.Data.EntityModels;

public class TransactionReversal
{
    public int Id { get; set; }
    public int WalletTransactionId { get; set; }
    public int RequestedByStaffUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public int? ReviewedByStaffUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReversalReference { get; set; }

    public WalletTransaction WalletTransaction { get; set; } = null!;
    public StaffUser RequestedByStaffUser { get; set; } = null!;
    public StaffUser? ReviewedByStaffUser { get; set; }
}


