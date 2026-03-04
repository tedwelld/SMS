namespace SMS.Data.EntityModels;

public class StaffCashUp
{
    public int Id { get; set; }
    public int StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
    public decimal EcoCashTotal { get; set; }
    public decimal Total { get; set; }
    public int TransactionCount { get; set; }
    public DateTime SubmittedAt { get; set; }

    public StaffUser? StaffUser { get; set; }
}
