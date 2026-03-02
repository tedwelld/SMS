namespace SMS.Data.EntityModels;

public class SalesTrendPoint
{
    public int Id { get; set; }
    public string Hour { get; set; } = string.Empty;
    public decimal Sales { get; set; }
}

