namespace SMS.Data.EntityModels;

public class AirtimePurchase
{
    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public string Network { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AirtimeBalanceAfter { get; set; }
    public string VoucherCode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public CustomerAccount CustomerAccount { get; set; } = null!;
}


