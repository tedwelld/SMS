using Microsoft.EntityFrameworkCore;
using SMS.Data.Enums;
using SMS.Data.EntityModels;

namespace SMS.Data.DbContext;

public class SmsDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public SmsDbContext(DbContextOptions<SmsDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<AccessMethod> AccessMethods => Set<AccessMethod>();
    public DbSet<NfcCard> NfcCards => Set<NfcCard>();
    public DbSet<QrToken> QrTokens => Set<QrToken>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<MobileAppSession> MobileAppSessions => Set<MobileAppSession>();
    public DbSet<SmsNotification> SmsNotifications => Set<SmsNotification>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<AccountNotification> AccountNotifications => Set<AccountNotification>();
    public DbSet<CommunicationMessage> CommunicationMessages => Set<CommunicationMessage>();
    public DbSet<ActivitySnapshot> ActivitySnapshots => Set<ActivitySnapshot>();
    public DbSet<MerchantTillPayment> MerchantTillPayments => Set<MerchantTillPayment>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<AgentFloatTransaction> AgentFloatTransactions => Set<AgentFloatTransaction>();
    public DbSet<TransactionReversal> TransactionReversals => Set<TransactionReversal>();
    public DbSet<AirtimePurchase> AirtimePurchases => Set<AirtimePurchase>();
    public DbSet<RetailSetting> RetailSettings => Set<RetailSetting>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorDepartment> VendorDepartments => Set<VendorDepartment>();
    public DbSet<SalesTrendPoint> SalesTrendPoints => Set<SalesTrendPoint>();
    public DbSet<PosPayment> PosPayments => Set<PosPayment>();
    public DbSet<PosPaymentLine> PosPaymentLines => Set<PosPaymentLine>();
    public DbSet<StaffCashUp> StaffCashUps => Set<StaffCashUp>();
    public DbSet<RetailAuditLog> RetailAuditLogs => Set<RetailAuditLog>();
    public DbSet<CustomerPurchaseRecord> CustomerPurchaseRecords => Set<CustomerPurchaseRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.ProfileImageUrl).HasMaxLength(4000);
            entity.Property(x => x.NotificationsEnabled).HasDefaultValue(true);
            entity.Property(x => x.PreferredLanguage).HasMaxLength(20).HasDefaultValue("en");
            entity.Property(x => x.DailyTransferLimit).HasColumnType("decimal(18,2)").HasDefaultValue(2000m);
            entity.Property(x => x.LoyaltyPoints).HasDefaultValue(0);
            entity.Property(x => x.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<CustomerAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => x.CustomerId).IsUnique();
            entity.HasIndex(x => x.AccountNumber).IsUnique();

            entity.HasOne(x => x.Customer)
                .WithOne(x => x.Account)
                .HasForeignKey<CustomerAccount>(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.CustomerAccountId).IsUnique();

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.Wallets)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessMethod>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DateCreated).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Wallet)
                .WithMany(x => x.AccessMethods)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NfcCard>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CardUid).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.CardUid).IsUnique();
            entity.HasIndex(x => x.PhoneNumber).IsUnique();

            entity.HasOne(x => x.Wallet)
                .WithMany(x => x.NfcCards)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QrToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Pin).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Token).IsUnique();

            entity.ToTable(t => t.HasCheckConstraint("CK_QrTokens_Usage", "[CurrentUsage] <= [MaxUsage] AND [CurrentUsage] >= 0"));

            entity.HasOne(x => x.Wallet)
                .WithMany(x => x.QrTokens)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30);
            entity.Property(x => x.Department).HasMaxLength(100);
            entity.Property(x => x.NotificationsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CanApproveReversals).HasDefaultValue(true);
            entity.Property(x => x.ProfileImageUrl).HasMaxLength(4000);
            entity.Property(x => x.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Timestamp).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.StaffUser)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Wallet)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MobileAppSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JwtToken).HasMaxLength(2048).IsRequired();

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.MobileAppSessions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.QrToken)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.QrTokenId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SmsNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.SentAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.SmsNotifications)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TransactionType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CounterpartyPhoneNumber).HasMaxLength(30);
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerAccountId, x.CreatedAt });

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerId, x.CreatedAt });

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WalletTransaction)
                .WithMany()
                .HasForeignKey(x => x.WalletTransactionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CommunicationMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.SenderType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RecipientType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.RecipientCustomerId, x.RecipientStaffUserId, x.CreatedAt });
            entity.HasIndex(x => new { x.SenderCustomerId, x.SenderStaffUserId, x.CreatedAt });

            entity.HasOne(x => x.SenderCustomer)
                .WithMany(x => x.SentCommunicationMessages)
                .HasForeignKey(x => x.SenderCustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RecipientCustomer)
                .WithMany(x => x.ReceivedCommunicationMessages)
                .HasForeignKey(x => x.RecipientCustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SenderStaffUser)
                .WithMany(x => x.SentCommunicationMessages)
                .HasForeignKey(x => x.SenderStaffUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RecipientStaffUser)
                .WithMany(x => x.ReceivedCommunicationMessages)
                .HasForeignKey(x => x.RecipientStaffUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ActivitySnapshot>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Details).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<MerchantTillPayment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MerchantName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TillNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerAccountId, x.CreatedAt });

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.MerchantTillPayments)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillPayment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BillerCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.BillerName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.AccountReference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerAccountId, x.CreatedAt });

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.BillPayments)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentFloatTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TransactionType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerAccountId, x.CreatedAt });

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.AgentFloatTransactions)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransactionReversal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RequestedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.ReviewNotes).HasMaxLength(1000);
            entity.Property(x => x.ReversalReference).HasMaxLength(120);
            entity.HasIndex(x => x.WalletTransactionId);
            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.WalletTransaction)
                .WithMany(x => x.ReversalRequests)
                .HasForeignKey(x => x.WalletTransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RequestedByStaffUser)
                .WithMany(x => x.RequestedReversals)
                .HasForeignKey(x => x.RequestedByStaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedByStaffUser)
                .WithMany(x => x.ReviewedReversals)
                .HasForeignKey(x => x.ReviewedByStaffUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AirtimePurchase>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Network).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AirtimeBalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(x => x.VoucherCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerAccountId, x.CreatedAt });

            entity.HasOne(x => x.CustomerAccount)
                .WithMany(x => x.AirtimePurchases)
                .HasForeignKey(x => x.CustomerAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RetailSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActiveRole).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(32);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("decimal(8,4)");
            entity.Property(x => x.PromoType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PromoValue).HasColumnType("decimal(8,2)");
            entity.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Contact).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<VendorDepartment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Department).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.VendorId, x.Department }).IsUnique();
            entity.HasOne(x => x.Vendor)
                .WithMany(x => x.Departments)
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SalesTrendPoint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Hour).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Sales).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => x.Hour).IsUnique();
        });

        modelBuilder.Entity<PosPayment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalTransactionId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ProcessedByName).HasMaxLength(200);
            entity.Property(x => x.CustomerPhone).HasMaxLength(30);
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Tax).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.ExternalTransactionId);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => new { x.StaffUserId, x.Timestamp });
            entity.HasOne(x => x.StaffUser)
                .WithMany()
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PosPaymentLine>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductId).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Tax).HasColumnType("decimal(18,2)");
            entity.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => x.PosPaymentId);
            entity.HasOne(x => x.PosPayment)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.PosPaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RetailAuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UserRole).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<StaffCashUp>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StaffName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BusinessDate).HasColumnType("date");
            entity.Property(x => x.CashTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CardTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.EcoCashTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Total).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SubmittedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.StaffUserId, x.BusinessDate }).IsUnique();
            entity.HasIndex(x => x.BusinessDate);
            entity.HasOne(x => x.StaffUser)
                .WithMany()
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerPurchaseRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalTransactionId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Total).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Date).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => new { x.CustomerId, x.Date });
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.PurchaseHistory)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}


