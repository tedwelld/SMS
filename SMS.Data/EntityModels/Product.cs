using SMS.Data.Enums;

namespace SMS.Data.EntityModels;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; }
    public decimal TaxRate { get; set; }
    public bool Staple { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public PromotionType PromoType { get; set; }
    public decimal PromoValue { get; set; }
    public int PhysicalCount { get; set; }
}

