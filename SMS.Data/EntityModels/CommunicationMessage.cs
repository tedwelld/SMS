namespace SMS.Data.EntityModels;

public class CommunicationMessage
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public int? SenderCustomerId { get; set; }
    public int? SenderStaffUserId { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public int? RecipientCustomerId { get; set; }
    public int? RecipientStaffUserId { get; set; }
    public bool IsReadByRecipient { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool DeletedBySender { get; set; }
    public bool DeletedByRecipient { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer? SenderCustomer { get; set; }
    public StaffUser? SenderStaffUser { get; set; }
    public Customer? RecipientCustomer { get; set; }
    public StaffUser? RecipientStaffUser { get; set; }
}


