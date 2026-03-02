using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Core.Services;

public class CustomerService(SmsDbContext db) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Customers.AsNoTracking()
            .Select(x => new CustomerDto(x.Id, x.PhoneNumber, x.Email, x.Name, x.IsActive, x.DateCreated))
            .ToListAsync(cancellationToken);

    public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null
            ? null
            : new CustomerDto(entity.Id, entity.PhoneNumber, entity.Email, entity.Name, entity.IsActive, entity.DateCreated);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var phoneNumber = request.PhoneNumber.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? $"{phoneNumber}@tarwallet.local" : request.Email.Trim();
        var password = string.IsNullOrWhiteSpace(request.Password)
            ? Guid.NewGuid().ToString("N")
            : request.Password.Trim();

        var entity = new Customer
        {
            Name = request.Name.Trim(),
            PhoneNumber = phoneNumber,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
            DateCreated = DateTime.UtcNow
        };

        db.Customers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerDto(entity.Id, entity.PhoneNumber, entity.Email, entity.Name, entity.IsActive, entity.DateCreated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Customers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class CustomerAccountService(SmsDbContext db) : ICustomerAccountService
{
    public async Task<IReadOnlyList<CustomerAccountDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.CustomerAccounts.AsNoTracking()
            .Select(x => new CustomerAccountDto(x.Id, x.CustomerId, x.AccountNumber, x.Balance, x.IsFrozen))
            .ToListAsync(cancellationToken);

    public async Task<CustomerAccountDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.CustomerAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null
            ? null
            : new CustomerAccountDto(entity.Id, entity.CustomerId, entity.AccountNumber, entity.Balance, entity.IsFrozen);
    }

    public async Task<CustomerAccountDto> CreateAsync(CreateCustomerAccountRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new InvalidOperationException($"Customer {request.CustomerId} does not exist.");
        }

        var entity = new CustomerAccount
        {
            CustomerId = request.CustomerId,
            AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber)
                ? $"ACC-{request.CustomerId:D6}-{Random.Shared.Next(1000, 9999)}"
                : request.AccountNumber.Trim(),
            Balance = request.OpeningBalance,
            IsFrozen = request.IsFrozen
        };
        db.CustomerAccounts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerAccountDto(entity.Id, entity.CustomerId, entity.AccountNumber, entity.Balance, entity.IsFrozen);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.CustomerAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.CustomerAccounts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class WalletService(SmsDbContext db) : IWalletService
{
    public async Task<IReadOnlyList<WalletDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Wallets.AsNoTracking().Select(x => new WalletDto(x.Id, x.CustomerAccountId, x.IsActive, x.DateCreated)).ToListAsync(cancellationToken);

    public async Task<WalletDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new WalletDto(entity.Id, entity.CustomerAccountId, entity.IsActive, entity.DateCreated);
    }

    public async Task<WalletDto> CreateAsync(CreateWalletRequest request, CancellationToken cancellationToken = default)
    {
        var accountExists = await db.CustomerAccounts.AnyAsync(x => x.Id == request.CustomerAccountId, cancellationToken);
        if (!accountExists)
        {
            throw new InvalidOperationException($"CustomerAccount {request.CustomerAccountId} does not exist.");
        }

        var entity = new Wallet { CustomerAccountId = request.CustomerAccountId, IsActive = request.IsActive, DateCreated = DateTime.UtcNow };
        db.Wallets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new WalletDto(entity.Id, entity.CustomerAccountId, entity.IsActive, entity.DateCreated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Wallets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Wallets.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class AccessMethodService(SmsDbContext db) : IAccessMethodService
{
    public async Task<IReadOnlyList<AccessMethodDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.AccessMethods.AsNoTracking().Select(x => new AccessMethodDto(x.Id, x.WalletId, x.IsActive, x.DateCreated)).ToListAsync(cancellationToken);

    public async Task<AccessMethodDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.AccessMethods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new AccessMethodDto(entity.Id, entity.WalletId, entity.IsActive, entity.DateCreated);
    }

    public async Task<AccessMethodDto> CreateAsync(CreateAccessMethodRequest request, CancellationToken cancellationToken = default)
    {
        var walletExists = await db.Wallets.AnyAsync(x => x.Id == request.WalletId, cancellationToken);
        if (!walletExists)
        {
            throw new InvalidOperationException($"Wallet {request.WalletId} does not exist.");
        }

        var entity = new AccessMethod { WalletId = request.WalletId, IsActive = request.IsActive, DateCreated = DateTime.UtcNow };
        db.AccessMethods.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new AccessMethodDto(entity.Id, entity.WalletId, entity.IsActive, entity.DateCreated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.AccessMethods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.AccessMethods.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class NfcCardService(SmsDbContext db) : INfcCardService
{
    public async Task<IReadOnlyList<NfcCardDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.NfcCards.AsNoTracking().Select(x => new NfcCardDto(x.Id, x.WalletId, x.CardUid, x.PhoneNumber)).ToListAsync(cancellationToken);

    public async Task<NfcCardDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.NfcCards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new NfcCardDto(entity.Id, entity.WalletId, entity.CardUid, entity.PhoneNumber);
    }

    public async Task<NfcCardDto> CreateAsync(CreateNfcCardRequest request, CancellationToken cancellationToken = default)
    {
        var walletExists = await db.Wallets.AnyAsync(x => x.Id == request.WalletId, cancellationToken);
        if (!walletExists)
        {
            throw new InvalidOperationException($"Wallet {request.WalletId} does not exist.");
        }

        var entity = new NfcCard
        {
            WalletId = request.WalletId,
            CardUid = request.CardUid.Trim(),
            PhoneNumber = request.PhoneNumber.Trim()
        };
        db.NfcCards.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new NfcCardDto(entity.Id, entity.WalletId, entity.CardUid, entity.PhoneNumber);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.NfcCards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.NfcCards.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class QrTokenService(SmsDbContext db) : IQrTokenService
{
    public async Task<IReadOnlyList<QrTokenDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.QrTokens.AsNoTracking().Select(x => new QrTokenDto(x.Id, x.WalletId, x.Token, x.Expiry, x.MaxUsage, x.CurrentUsage, x.Pin)).ToListAsync(cancellationToken);

    public async Task<QrTokenDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.QrTokens.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new QrTokenDto(entity.Id, entity.WalletId, entity.Token, entity.Expiry, entity.MaxUsage, entity.CurrentUsage, entity.Pin);
    }

    public async Task<QrTokenDto> CreateAsync(CreateQrTokenRequest request, CancellationToken cancellationToken = default)
    {
        var walletExists = await db.Wallets.AnyAsync(x => x.Id == request.WalletId, cancellationToken);
        if (!walletExists)
        {
            throw new InvalidOperationException($"Wallet {request.WalletId} does not exist.");
        }

        var entity = new QrToken
        {
            WalletId = request.WalletId,
            Token = request.Token.Trim(),
            Expiry = request.Expiry,
            MaxUsage = request.MaxUsage,
            CurrentUsage = 0,
            Pin = request.Pin.Trim()
        };
        db.QrTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new QrTokenDto(entity.Id, entity.WalletId, entity.Token, entity.Expiry, entity.MaxUsage, entity.CurrentUsage, entity.Pin);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.QrTokens.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.QrTokens.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class StaffUserService(SmsDbContext db) : IStaffUserService
{
    public async Task<IReadOnlyList<StaffUserDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.StaffUsers.AsNoTracking()
            .Select(x => new StaffUserDto(x.Id, x.Username, x.Name, x.Email, x.Role, x.IsActive, x.DateCreated))
            .ToListAsync(cancellationToken);

    public async Task<StaffUserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.StaffUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null
            ? null
            : new StaffUserDto(entity.Id, entity.Username, entity.Name, entity.Email, entity.Role, entity.IsActive, entity.DateCreated);
    }

    public async Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new StaffUser
        {
            Username = request.Username.Trim(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? request.Username.Trim() : request.Name.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role.Trim(),
            IsActive = request.IsActive,
            DateCreated = DateTime.UtcNow
        };
        db.StaffUsers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new StaffUserDto(entity.Id, entity.Username, entity.Name, entity.Email, entity.Role, entity.IsActive, entity.DateCreated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.StaffUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.StaffUsers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class AuditLogService(SmsDbContext db) : IAuditLogService
{
    public async Task<IReadOnlyList<AuditLogDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.AuditLogs.AsNoTracking().Select(x => new AuditLogDto(x.Id, x.StaffUserId, x.WalletId, x.Action, x.Timestamp, x.Details)).ToListAsync(cancellationToken);

    public async Task<AuditLogDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new AuditLogDto(entity.Id, entity.StaffUserId, entity.WalletId, entity.Action, entity.Timestamp, entity.Details);
    }

    public async Task<AuditLogDto> CreateAsync(CreateAuditLogRequest request, CancellationToken cancellationToken = default)
    {
        var staffExists = await db.StaffUsers.AnyAsync(x => x.Id == request.StaffUserId, cancellationToken);
        if (!staffExists)
        {
            throw new InvalidOperationException($"StaffUser {request.StaffUserId} does not exist.");
        }

        var entity = new AuditLog
        {
            StaffUserId = request.StaffUserId,
            WalletId = request.WalletId,
            Action = request.Action.Trim(),
            Details = request.Details.Trim(),
            Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new AuditLogDto(entity.Id, entity.StaffUserId, entity.WalletId, entity.Action, entity.Timestamp, entity.Details);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.AuditLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.AuditLogs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class MobileAppSessionService(SmsDbContext db) : IMobileAppSessionService
{
    public async Task<IReadOnlyList<MobileAppSessionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.MobileAppSessions.AsNoTracking().Select(x => new MobileAppSessionDto(x.Id, x.CustomerId, x.QrTokenId, x.JwtToken, x.Expiry)).ToListAsync(cancellationToken);

    public async Task<MobileAppSessionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.MobileAppSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new MobileAppSessionDto(entity.Id, entity.CustomerId, entity.QrTokenId, entity.JwtToken, entity.Expiry);
    }

    public async Task<MobileAppSessionDto> CreateAsync(CreateMobileAppSessionRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new InvalidOperationException($"Customer {request.CustomerId} does not exist.");
        }

        var entity = new MobileAppSession
        {
            CustomerId = request.CustomerId,
            QrTokenId = request.QrTokenId,
            JwtToken = request.JwtToken.Trim(),
            Expiry = request.Expiry
        };
        db.MobileAppSessions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new MobileAppSessionDto(entity.Id, entity.CustomerId, entity.QrTokenId, entity.JwtToken, entity.Expiry);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.MobileAppSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.MobileAppSessions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class SmsNotificationService(SmsDbContext db) : ISmsNotificationService
{
    public async Task<IReadOnlyList<SmsNotificationDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.SmsNotifications.AsNoTracking().Select(x => new SmsNotificationDto(x.Id, x.CustomerId, x.SentAt, x.IsSuccess, x.Message)).ToListAsync(cancellationToken);

    public async Task<SmsNotificationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.SmsNotifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : new SmsNotificationDto(entity.Id, entity.CustomerId, entity.SentAt, entity.IsSuccess, entity.Message);
    }

    public async Task<SmsNotificationDto> CreateAsync(CreateSmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new InvalidOperationException($"Customer {request.CustomerId} does not exist.");
        }

        var entity = new SmsNotification
        {
            CustomerId = request.CustomerId,
            SentAt = request.SentAt,
            IsSuccess = request.IsSuccess,
            Message = request.Message.Trim()
        };
        db.SmsNotifications.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new SmsNotificationDto(entity.Id, entity.CustomerId, entity.SentAt, entity.IsSuccess, entity.Message);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.SmsNotifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SmsNotifications.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}


