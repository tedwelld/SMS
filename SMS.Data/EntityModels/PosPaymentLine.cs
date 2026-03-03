namespace SMS.Data.EntityModels;

public class PosPaymentLine
{
    public int Id { get; set; }
    public int PosPaymentId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal LineTotal { get; set; }
    public PosPayment? PosPayment { get; set; }
}
