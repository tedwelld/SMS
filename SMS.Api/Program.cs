using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SMS.Api.Infrastructure;
using SMS.Core.Interfaces;
using SMS.Core.Services;
using SMS.Data.DbContext;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "http://127.0.0.1:4200",
                "https://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string is missing.");

builder.Services.AddDbContext<SmsDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.MigrationsAssembly("SMS.Data");
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        sql.CommandTimeout(30);
    });

#if DEBUG
    options.EnableDetailedErrors();
    options.EnableSensitiveDataLogging();
#endif
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt options are missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 24)
{
    throw new InvalidOperationException("Jwt:SecretKey must be at least 24 characters.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(45)
        };
    });

builder.Services.AddAuthorization();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new
        {
            message = "Validation failed",
            errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => k.Key,
                    v => v.Value!.Errors.Select(e => e.ErrorMessage))
        });
});

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerAccountService, CustomerAccountService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IAccessMethodService, AccessMethodService>();
builder.Services.AddScoped<INfcCardService, NfcCardService>();
builder.Services.AddScoped<IQrTokenService, QrTokenService>();
builder.Services.AddScoped<IStaffUserService, StaffUserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IMobileAppSessionService, MobileAppSessionService>();
builder.Services.AddScoped<ISmsNotificationService, SmsNotificationService>();
builder.Services.AddScoped<ISmsRetailService, SmsRetailService>();
builder.Services.AddScoped<UserOnboardingService>();

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services);

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            message = "An unexpected server error occurred."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { ok = true, service = "sms-api" }));
app.MapControllers();

app.Run();
