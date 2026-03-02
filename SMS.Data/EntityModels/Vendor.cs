namespace SMS.Data.EntityModels;

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public string Contact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ICollection<VendorDepartment> Departments { get; set; } = new List<VendorDepartment>();
}

