namespace SMS.Data.EntityModels;

public class WalletTransaction
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? CounterpartyPhoneNumber { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public CustomerAccount CustomerAccount { get; set; } = null!;
    public ICollection<TransactionReversal> ReversalRequests { get; set; } = new List<TransactionReversal>();
}


