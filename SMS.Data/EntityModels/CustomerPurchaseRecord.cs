namespace SMS.Data.EntityModels;

public class CustomerPurchaseRecord
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public int PointsEarned { get; set; }

    public Customer Customer { get; set; } = null!;
}

