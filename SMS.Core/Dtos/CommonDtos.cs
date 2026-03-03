namespace SMS.Core.Dtos;

public class CustomerDto
{
    public CustomerDto(int id, string phoneNumber, string email, string name, bool isActive, DateTime dateCreated)
    {
        Id = id;
        PhoneNumber = phoneNumber;
        Email = email;
        Name = name;
        IsActive = isActive;
        DateCreated = dateCreated;
    }

    public int Id { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class CustomerAccountDto
{
    public CustomerAccountDto(int id, int customerId, string accountNumber, decimal balance, bool isFrozen)
    {
        Id = id;
        CustomerId = customerId;
        AccountNumber = accountNumber;
        Balance = balance;
        IsFrozen = isFrozen;
    }

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public bool IsFrozen { get; set; }
}

public class WalletDto
{
    public WalletDto(int id, int customerAccountId, bool isActive, DateTime dateCreated)
    {
        Id = id;
        CustomerAccountId = customerAccountId;
        IsActive = isActive;
        DateCreated = dateCreated;
    }

    public int Id { get; set; }
    public int CustomerAccountId { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class AccessMethodDto
{
    public AccessMethodDto(int id, int walletId, bool isActive, DateTime dateCreated)
    {
        Id = id;
        WalletId = walletId;
        IsActive = isActive;
        DateCreated = dateCreated;
    }

    public int Id { get; set; }
    public int WalletId { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class NfcCardDto
{
    public NfcCardDto(int id, int walletId, string cardUid, string phoneNumber)
    {
        Id = id;
        WalletId = walletId;
        CardUid = cardUid;
        PhoneNumber = phoneNumber;
    }

    public int Id { get; set; }
    public int WalletId { get; set; }
    public string CardUid { get; set; }
    public string PhoneNumber { get; set; }
}

public class QrTokenDto
{
    public QrTokenDto(int id, int walletId, string token, DateTime expiry, int maxUsage, int currentUsage, string pin)
    {
        Id = id;
        WalletId = walletId;
        Token = token;
        Expiry = expiry;
        MaxUsage = maxUsage;
        CurrentUsage = currentUsage;
        Pin = pin;
    }

    public int Id { get; set; }
    public int WalletId { get; set; }
    public string Token { get; set; }
    public DateTime Expiry { get; set; }
    public int MaxUsage { get; set; }
    public int CurrentUsage { get; set; }
    public string Pin { get; set; }
}

public class StaffUserDto
{
    public StaffUserDto(int id, string username, string name, string email, string role, bool isActive, DateTime dateCreated)
    {
        Id = id;
        Username = username;
        Name = name;
        Email = email;
        Role = role;
        IsActive = isActive;
        DateCreated = dateCreated;
    }

    public int Id { get; set; }
    public string Username { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class AuditLogDto
{
    public AuditLogDto(int id, int staffUserId, int? walletId, string action, DateTime timestamp, string details)
    {
        Id = id;
        StaffUserId = staffUserId;
        WalletId = walletId;
        Action = action;
        Timestamp = timestamp;
        Details = details;
    }

    public int Id { get; set; }
    public int StaffUserId { get; set; }
    public int? WalletId { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
    public string Details { get; set; }
}

public class MobileAppSessionDto
{
    public MobileAppSessionDto(int id, int customerId, int? qrTokenId, string jwtToken, DateTime expiry)
    {
        Id = id;
        CustomerId = customerId;
        QrTokenId = qrTokenId;
        JwtToken = jwtToken;
        Expiry = expiry;
    }

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? QrTokenId { get; set; }
    public string JwtToken { get; set; }
    public DateTime Expiry { get; set; }
}

public class SmsNotificationDto
{
    public SmsNotificationDto(int id, int customerId, DateTime sentAt, bool isSuccess, string message)
    {
        Id = id;
        CustomerId = customerId;
        SentAt = sentAt;
        IsSuccess = isSuccess;
        Message = message;
    }

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
}


public class CreateCustomerRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CreateCustomerAccountRequest
{
    public int CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public bool IsFrozen { get; set; }
}

public class CreateWalletRequest
{
    public int CustomerAccountId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAccessMethodRequest
{
    public int WalletId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateNfcCardRequest
{
    public int WalletId { get; set; }
    public string CardUid { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class CreateQrTokenRequest
{
    public int WalletId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public int MaxUsage { get; set; }
    public string Pin { get; set; } = string.Empty;
}

public class CreateStaffUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class CreateAuditLogRequest
{
    public int StaffUserId { get; set; }
    public int? WalletId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public class CreateMobileAppSessionRequest
{
    public int CustomerId { get; set; }
    public int? QrTokenId { get; set; }
    public string JwtToken { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}

public class CreateSmsNotificationRequest
{
    public int CustomerId { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}


