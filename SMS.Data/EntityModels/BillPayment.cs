namespace SMS.Data.EntityModels;

public class BillPayment
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public string BillerCode { get; set; } = string.Empty;
    public string BillerName { get; set; } = string.Empty;
    public string AccountReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public CustomerAccount CustomerAccount { get; set; } = null!;
}


