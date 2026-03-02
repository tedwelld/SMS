namespace SMS.Data.EntityModels;

public class RetailAuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

