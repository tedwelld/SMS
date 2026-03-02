namespace SMS.Data.EntityModels;

public class RetailSetting
{
    public int Id { get; set; }
    public string ActiveRole { get; set; } = "Store Manager";
    public bool OfflineMode { get; set; }
}

