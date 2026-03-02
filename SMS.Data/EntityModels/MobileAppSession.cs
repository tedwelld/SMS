namespace SMS.Data.EntityModels;

public class MobileAppSession
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? QrTokenId { get; set; }
    public string JwtToken { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }

    public Customer Customer { get; set; } = null!;
    public QrToken? QrToken { get; set; }
}


