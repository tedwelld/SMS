namespace SMS.Data.EntityModels;

public class AccessMethod
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }

    public Wallet Wallet { get; set; } = null!;
}


