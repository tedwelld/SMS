using Microsoft.EntityFrameworkCore;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Infrastructure;

public class UserOnboardingService(SmsDbContext db)
{
    public async Task<OnboardedUserResult> CreateUserAsync(
        string phoneNumber,
        string name,
        string email,
        string password,
        decimal openingBalance,
        bool isFrozen,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedName = name.Trim();

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new InvalidOperationException("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters long.");
        }

        if (await db.Customers.AnyAsync(x => x.PhoneNumber == normalizedPhone, cancellationToken))
        {
            throw new InvalidOperationException("Phone number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await db.Customers.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var now = DateTime.UtcNow;

        var customer = new Customer
        {
            PhoneNumber = normalizedPhone,
            Name = normalizedName,
            Email = string.IsNullOrWhiteSpace(normalizedEmail) ? $"{normalizedPhone}@tarwallet.local" : normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.Trim()),
            IsActive = true,
            NotificationsEnabled = true,
            PreferredLanguage = "en",
            DailyTransferLimit = 2000m,
            DateCreated = now
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        var account = new CustomerAccount
        {
            CustomerId = customer.Id,
            AccountNumber = BuildAccountNumber(normalizedPhone),
            Balance = openingBalance,
            IsFrozen = isFrozen
        };
        db.CustomerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        var wallet = new Wallet
        {
            CustomerAccountId = account.Id,
            IsActive = true,
            DateCreated = now
        };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);

        var nfcCard = new NfcCard
        {
            WalletId = wallet.Id,
            CardUid = BuildNfcCardUid(normalizedPhone),
            PhoneNumber = normalizedPhone
        };

        var accessMethod = new AccessMethod
        {
            WalletId = wallet.Id,
            IsActive = true,
            DateCreated = now
        };

        var qrToken = new QrToken
        {
            WalletId = wallet.Id,
            Token = Guid.NewGuid().ToString("N"),
            Expiry = now.AddDays(30),
            MaxUsage = 500,
            CurrentUsage = 0,
            Pin = Random.Shared.Next(1000, 9999).ToString()
        };

        var welcomeSms = new SmsNotification
        {
            CustomerId = customer.Id,
            SentAt = now,
            IsSuccess = true,
            Message = "Welcome to Tar Digital Wallet. Your wallet is ready."
        };

        db.NfcCards.Add(nfcCard);
        db.AccessMethods.Add(accessMethod);
        db.QrTokens.Add(qrToken);
        db.SmsNotifications.Add(welcomeSms);
        await db.SaveChangesAsync(cancellationToken);
        return new OnboardedUserResult(customer, account, wallet, nfcCard, qrToken);
    }

    public static string NormalizePhone(string phoneNumber) =>
        string.Concat(phoneNumber.Trim().Where(ch => char.IsDigit(ch) || ch == '+'));

    private static string BuildAccountNumber(string normalizedPhone)
    {
        var digits = new string(normalizedPhone.Where(char.IsDigit).ToArray());
        var suffix = digits.Length <= 8 ? digits : digits[^8..];
        return $"TDW{suffix}{Random.Shared.Next(100, 999)}";
    }

    private static string BuildNfcCardUid(string normalizedPhone)
    {
        var digits = new string(normalizedPhone.Where(char.IsDigit).ToArray());
        var suffix = digits.Length <= 6 ? digits : digits[^6..];
        return $"NFC-{suffix}-{Guid.NewGuid():N}"[..24];
    }
}

public sealed record OnboardedUserResult(Customer Customer, CustomerAccount Account, Wallet Wallet, NfcCard NfcCard, QrToken QrToken);


