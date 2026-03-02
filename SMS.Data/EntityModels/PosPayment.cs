using SMS.Data.Enums;

namespace SMS.Data.EntityModels;

public class PosPayment
{
    public int Id { get; set; }
    public PosPaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

