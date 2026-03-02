namespace SMS.Data.EntityModels;

public class NfcCard
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public string CardUid { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public Wallet Wallet { get; set; } = null!;
}


