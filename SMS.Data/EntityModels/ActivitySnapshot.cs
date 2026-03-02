namespace SMS.Data.EntityModels;

public class ActivitySnapshot
{
    public int Id { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public int? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}


