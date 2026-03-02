using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SMS.Data.DbContext;

namespace SMS.Data.DbContext;

public class SmsDbContextFactory : IDesignTimeDbContextFactory<SmsDbContext>
{
    public SmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SmsDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("SMS_DEFAULT_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SMSDb;Trusted_Connection=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(SmsDbContext).Assembly.FullName);
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sql.CommandTimeout(30);
        });

        return new SmsDbContext(optionsBuilder.Options);
    }
}


