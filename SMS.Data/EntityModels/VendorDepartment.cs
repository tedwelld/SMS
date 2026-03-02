namespace SMS.Data.EntityModels;

public class VendorDepartment
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string Department { get; set; } = string.Empty;

    public Vendor Vendor { get; set; } = null!;
}

