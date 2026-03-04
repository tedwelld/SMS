using SMS.Data.Enums;

namespace SMS.Data.EntityModels;

public class PosPayment
{
    public int Id { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public PosPaymentMethod Method { get; set; }
    public int? StaffUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public int PointsRedeemed { get; set; }
    public int PointsEarned { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public List<PosPaymentLine> Lines { get; set; } = [];

    public StaffUser? StaffUser { get; set; }
}

