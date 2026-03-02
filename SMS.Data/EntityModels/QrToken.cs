namespace SMS.Data.EntityModels;

public class QrToken
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public int MaxUsage { get; set; }
    public int CurrentUsage { get; set; }
    public string Pin { get; set; } = string.Empty;

    public Wallet Wallet { get; set; } = null!;
    public ICollection<MobileAppSession> Sessions { get; set; } = new List<MobileAppSession>();
}


