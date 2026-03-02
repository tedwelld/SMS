namespace SMS.Data.EntityModels;

public class Customer
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public decimal DailyTransferLimit { get; set; }
    public int LoyaltyPoints { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public CustomerAccount? Account { get; set; }
    public ICollection<SmsNotification> SmsNotifications { get; set; } = new List<SmsNotification>();
    public ICollection<AccountNotification> Notifications { get; set; } = new List<AccountNotification>();
    public ICollection<MobileAppSession> MobileAppSessions { get; set; } = new List<MobileAppSession>();
    public ICollection<CustomerPurchaseRecord> PurchaseHistory { get; set; } = new List<CustomerPurchaseRecord>();
    public ICollection<CommunicationMessage> SentCommunicationMessages { get; set; } = new List<CommunicationMessage>();
    public ICollection<CommunicationMessage> ReceivedCommunicationMessages { get; set; } = new List<CommunicationMessage>();
}


