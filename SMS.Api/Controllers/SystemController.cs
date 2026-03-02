using Microsoft.AspNetCore.Mvc;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private static readonly object Sync = new();
    private static SystemSettingsDto Settings = BuildDefaultSettings();
    private static readonly List<PermissionTemplateDto> Templates = BuildDefaultTemplates();
    private static readonly Dictionary<string, StaffPermissionsDto> StaffPermissions = new();

    [HttpGet("settings")]
    public ActionResult<SystemSettingsDto> GetSettings()
    {
        lock (Sync)
        {
            return Ok(CloneSettings(Settings));
        }
    }

    [HttpPut("settings")]
    public ActionResult<SystemSettingsDto> UpdateSettings([FromBody] SystemSettingsDto request)
    {
        lock (Sync)
        {
            Settings = request;
            Settings.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(Settings.UpdatedBy))
            {
                Settings.UpdatedBy = "system";
            }

            return Ok(CloneSettings(Settings));
        }
    }

    [HttpGet("permission-templates")]
    public ActionResult<IReadOnlyList<PermissionTemplateDto>> GetPermissionTemplates()
    {
        lock (Sync)
        {
            return Ok(Templates.Select(CloneTemplate).ToList());
        }
    }

    [HttpPost("permission-templates")]
    public ActionResult<PermissionTemplateDto> CreatePermissionTemplate([FromBody] PermissionTemplateDto request)
    {
        lock (Sync)
        {
            var template = CloneTemplate(request);
            template.Id = string.IsNullOrWhiteSpace(template.Id)
                ? Guid.NewGuid().ToString("N")
                : template.Id;
            template.IsDefault = false;
            Templates.Add(template);
            return Ok(CloneTemplate(template));
        }
    }

    [HttpPut("permission-templates/{id}")]
    public ActionResult<PermissionTemplateDto> UpdatePermissionTemplate(string id, [FromBody] PermissionTemplateDto request)
    {
        lock (Sync)
        {
            var existing = Templates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return NotFound(new { message = "Template not found." });
            }

            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.Permissions = new Dictionary<string, bool>(request.Permissions, StringComparer.OrdinalIgnoreCase);
            return Ok(CloneTemplate(existing));
        }
    }

    [HttpDelete("permission-templates/{id}")]
    public ActionResult<object> DeletePermissionTemplate(string id)
    {
        lock (Sync)
        {
            var existing = Templates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return NotFound(new { success = false, message = "Template not found." });
            }

            if (existing.IsDefault)
            {
                return BadRequest(new { success = false, message = "Default template cannot be deleted." });
            }

            Templates.Remove(existing);
            return Ok(new { success = true });
        }
    }

    [HttpGet("staff-permissions/{staffId}")]
    public ActionResult<StaffPermissionsDto> GetStaffPermissions(string staffId)
    {
        lock (Sync)
        {
            if (!StaffPermissions.TryGetValue(staffId, out var stored))
            {
                var fallbackTemplate = Templates.FirstOrDefault(x => x.IsDefault) ?? Templates.First();
                stored = new StaffPermissionsDto
                {
                    StaffId = staffId,
                    TemplateId = fallbackTemplate.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                StaffPermissions[staffId] = stored;
            }

            return Ok(CloneStaffPermissions(stored));
        }
    }

    [HttpPut("staff-permissions/{staffId}")]
    public ActionResult<StaffPermissionsDto> UpdateStaffPermissions(string staffId, [FromBody] StaffPermissionsDto request)
    {
        lock (Sync)
        {
            if (!StaffPermissions.TryGetValue(staffId, out var existing))
            {
                existing = new StaffPermissionsDto
                {
                    StaffId = staffId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                StaffPermissions[staffId] = existing;
            }

            existing.TemplateId = string.IsNullOrWhiteSpace(request.TemplateId)
                ? existing.TemplateId
                : request.TemplateId;
            existing.CustomPermissions = request.CustomPermissions is null
                ? null
                : new Dictionary<string, bool>(request.CustomPermissions, StringComparer.OrdinalIgnoreCase);
            existing.UpdatedAt = DateTime.UtcNow;

            return Ok(CloneStaffPermissions(existing));
        }
    }

    private static SystemSettingsDto BuildDefaultSettings()
    {
        var defaultTemplate = BuildDefaultTemplates().First();
        return new SystemSettingsDto
        {
            Id = "system-default",
            SystemName = "tar-digital",
            PrimaryColor = "#1a237e",
            SecondaryColor = "#283593",
            ReceiptHeader = "Thank you for your business!",
            ReceiptFooter = "This is an electronic receipt",
            ReceiptLogo = true,
            PrintAutomatically = true,
            ReceiptTimeout = 30,
            DefaultCurrency = "USD",
            CurrencySymbol = "$",
            CurrencyPosition = "before",
            DecimalPlaces = 2,
            ThousandSeparator = ",",
            MaxPaymentAmount = 10000,
            MaxDepositAmount = 5000,
            MaxWithdrawalAmount = 2000,
            DailyTransactionLimit = 50000,
            DefaultEventDuration = 24,
            AutoArchiveEvents = true,
            ArchiveAfterDays = 30,
            MaxLoginAttempts = 5,
            LockoutDuration = 15,
            SessionTimeout = 30,
            PasswordExpiryDays = 90,
            PermissionsTemplate = defaultTemplate,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "system"
        };
    }

    private static List<PermissionTemplateDto> BuildDefaultTemplates() =>
    [
        new PermissionTemplateDto
        {
            Id = "admin-template",
            Name = "Administrator",
            Description = "Full system access",
            Permissions = BuildDefaultPermissionSet(true),
            IsDefault = true
        },
        new PermissionTemplateDto
        {
            Id = "staff-template",
            Name = "Staff",
            Description = "Basic operations",
            Permissions = BuildDefaultPermissionSet(false),
            IsDefault = false
        }
    ];

    private static Dictionary<string, bool> BuildDefaultPermissionSet(bool admin)
    {
        var all = new[]
        {
            "viewDashboard", "exportDashboard", "viewWallets", "createWallets", "updateWallets", "suspendWallets", "deleteWallets",
            "viewCards", "issueCards", "updateCardStatus", "generateQrCodes", "revokeQrCodes",
            "viewTransactions", "filterTransactions", "exportTransactions", "processRefunds",
            "viewStaff", "createStaff", "updateStaff", "changeStaffRoles", "deactivateStaff",
            "viewSettings", "updateSettings", "viewAuditLogs",
            "usePOS", "processPayments", "processDeposits", "checkBalances", "printReceipts",
            "overrideLimits", "voidTransactions", "approveLargeTransactions"
        };

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in all)
        {
            result[key] = admin;
        }

        if (!admin)
        {
            result["viewDashboard"] = false;
            result["exportDashboard"] = false;
            result["viewStaff"] = false;
            result["createStaff"] = false;
            result["updateStaff"] = false;
            result["changeStaffRoles"] = false;
            result["deactivateStaff"] = false;
            result["viewSettings"] = false;
            result["updateSettings"] = false;
            result["viewAuditLogs"] = false;
            result["overrideLimits"] = false;
            result["voidTransactions"] = false;
            result["approveLargeTransactions"] = false;
            result["deleteWallets"] = false;
            result["processRefunds"] = false;
        }

        return result;
    }

    private static SystemSettingsDto CloneSettings(SystemSettingsDto source) =>
        new()
        {
            Id = source.Id,
            SystemName = source.SystemName,
            LogoUrl = source.LogoUrl,
            PrimaryColor = source.PrimaryColor,
            SecondaryColor = source.SecondaryColor,
            FaviconUrl = source.FaviconUrl,
            ReceiptHeader = source.ReceiptHeader,
            ReceiptFooter = source.ReceiptFooter,
            ReceiptLogo = source.ReceiptLogo,
            PrintAutomatically = source.PrintAutomatically,
            ReceiptTimeout = source.ReceiptTimeout,
            DefaultCurrency = source.DefaultCurrency,
            CurrencySymbol = source.CurrencySymbol,
            CurrencyPosition = source.CurrencyPosition,
            DecimalPlaces = source.DecimalPlaces,
            ThousandSeparator = source.ThousandSeparator,
            MaxPaymentAmount = source.MaxPaymentAmount,
            MaxDepositAmount = source.MaxDepositAmount,
            MaxWithdrawalAmount = source.MaxWithdrawalAmount,
            DailyTransactionLimit = source.DailyTransactionLimit,
            DefaultEventDuration = source.DefaultEventDuration,
            AutoArchiveEvents = source.AutoArchiveEvents,
            ArchiveAfterDays = source.ArchiveAfterDays,
            MaxLoginAttempts = source.MaxLoginAttempts,
            LockoutDuration = source.LockoutDuration,
            SessionTimeout = source.SessionTimeout,
            PasswordExpiryDays = source.PasswordExpiryDays,
            PermissionsTemplate = CloneTemplate(source.PermissionsTemplate),
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy
        };

    private static PermissionTemplateDto CloneTemplate(PermissionTemplateDto source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Permissions = new Dictionary<string, bool>(source.Permissions, StringComparer.OrdinalIgnoreCase),
            IsDefault = source.IsDefault
        };

    private static StaffPermissionsDto CloneStaffPermissions(StaffPermissionsDto source) =>
        new()
        {
            StaffId = source.StaffId,
            TemplateId = source.TemplateId,
            CustomPermissions = source.CustomPermissions is null
                ? null
                : new Dictionary<string, bool>(source.CustomPermissions, StringComparer.OrdinalIgnoreCase),
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };

    public sealed class SystemSettingsDto
    {
        public string Id { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string PrimaryColor { get; set; } = string.Empty;
        public string SecondaryColor { get; set; } = string.Empty;
        public string? FaviconUrl { get; set; }
        public string ReceiptHeader { get; set; } = string.Empty;
        public string ReceiptFooter { get; set; } = string.Empty;
        public bool ReceiptLogo { get; set; }
        public bool PrintAutomatically { get; set; }
        public int ReceiptTimeout { get; set; }
        public string DefaultCurrency { get; set; } = "USD";
        public string CurrencySymbol { get; set; } = "$";
        public string CurrencyPosition { get; set; } = "before";
        public int DecimalPlaces { get; set; } = 2;
        public string ThousandSeparator { get; set; } = ",";
        public decimal MaxPaymentAmount { get; set; }
        public decimal MaxDepositAmount { get; set; }
        public decimal MaxWithdrawalAmount { get; set; }
        public decimal DailyTransactionLimit { get; set; }
        public int DefaultEventDuration { get; set; }
        public bool AutoArchiveEvents { get; set; }
        public int ArchiveAfterDays { get; set; }
        public int MaxLoginAttempts { get; set; }
        public int LockoutDuration { get; set; }
        public int SessionTimeout { get; set; }
        public int PasswordExpiryDays { get; set; }
        public PermissionTemplateDto PermissionsTemplate { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
    }

    public sealed class PermissionTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, bool> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsDefault { get; set; }
    }

    public sealed class StaffPermissionsDto
    {
        public string StaffId { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public Dictionary<string, bool>? CustomPermissions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}


