namespace SMS.Data.EntityModels;

public class SmsNotification
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;

    public Customer Customer { get; set; } = null!;
}


