namespace SMS.Data.EntityModels;

public class Wallet
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }

    public CustomerAccount CustomerAccount { get; set; } = null!;
    public ICollection<AccessMethod> AccessMethods { get; set; } = new List<AccessMethod>();
    public ICollection<NfcCard> NfcCards { get; set; } = new List<NfcCard>();
    public ICollection<QrToken> QrTokens { get; set; } = new List<QrToken>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}


