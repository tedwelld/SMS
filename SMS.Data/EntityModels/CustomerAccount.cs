namespace SMS.Data.EntityModels;

public class CustomerAccount
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    public ICollection<MerchantTillPayment> MerchantTillPayments { get; set; } = new List<MerchantTillPayment>();
    public ICollection<BillPayment> BillPayments { get; set; } = new List<BillPayment>();
    public ICollection<AgentFloatTransaction> AgentFloatTransactions { get; set; } = new List<AgentFloatTransaction>();
    public ICollection<AirtimePurchase> AirtimePurchases { get; set; } = new List<AirtimePurchase>();
}


