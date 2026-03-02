namespace SMS.Data.EntityModels;

public class AgentFloatTransaction
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public CustomerAccount CustomerAccount { get; set; } = null!;
}


