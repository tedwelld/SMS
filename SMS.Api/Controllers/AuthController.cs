using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMS.Api.Infrastructure;
using SMS.Core.Dtos;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    SmsDbContext db,
    UserOnboardingService onboardingService,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await onboardingService.CreateUserAsync(
                request.PhoneNumber,
                request.Name,
                request.Email ?? string.Empty,
                request.Password,
                openingBalance: 0m,
                isFrozen: false,
                cancellationToken);

            return Ok(await CreateUserAuthResponseAsync(user.Customer, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> LoginUser([FromBody] UserLoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedPhone = UserOnboardingService.NormalizePhone(request.PhoneNumber);
        var digitsOnlyPhone = string.Concat(normalizedPhone.Where(char.IsDigit));
        var plusPrefixedPhone = string.IsNullOrWhiteSpace(digitsOnlyPhone) ? string.Empty : $"+{digitsOnlyPhone}";
        var customer = await db.Customers.FirstOrDefaultAsync(
            x => x.PhoneNumber == normalizedPhone
                || x.PhoneNumber == digitsOnlyPhone
                || x.PhoneNumber == plusPrefixedPhone,
            cancellationToken);
        var validHash = customer is not null && !string.IsNullOrWhiteSpace(customer.PasswordHash) && customer.PasswordHash.StartsWith("$2");
        var password = request.Password?.Trim() ?? string.Empty;
        if (customer is null || !customer.IsActive || !validHash || !BCrypt.Net.BCrypt.Verify(password, customer.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid phone number or password." });
        }

        customer.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await CreateUserAuthResponseAsync(customer, cancellationToken));
    }

    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> LoginAdmin([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = "Invalid admin credentials." });
        }

        var lookup = request.UsernameOrEmail.Trim();
        var normalizedLookup = lookup.ToUpperInvariant();
        var staff = await db.StaffUsers.FirstOrDefaultAsync(
            x => x.Username.ToUpper() == normalizedLookup || x.Email.ToUpper() == normalizedLookup,
            cancellationToken);

        var validHash = staff is not null && !string.IsNullOrWhiteSpace(staff.PasswordHash) && staff.PasswordHash.StartsWith("$2");
        if (staff is null || !staff.IsActive || !validHash || !BCrypt.Net.BCrypt.Verify(request.Password, staff.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid admin credentials." });
        }

        var role = NormalizeStaffRole(staff.Role);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.TokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, staff.Name),
            new(ClaimTypes.Email, staff.Email),
            new("staff_id", staff.Id.ToString())
        };

        var token = JwtTokenFactory.CreateToken(_jwtOptions, claims, expiresAt);
        var response = new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            Identity = new AuthIdentityDto
            {
                StaffUserId = staff.Id,
                Role = role,
                DisplayName = staff.Name,
                Email = staff.Email
            }
        };

        return Ok(response);
    }

    [HttpPost("create-account")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> CreateStaffAccount([FromBody] CreateStaffAccountRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Trim().Length < 6)
        {
            return BadRequest(new { message = "Provide valid username, name, email and password (minimum 6 chars)." });
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await db.StaffUsers.AnyAsync(
            x => x.Username.ToLower() == username.ToLower() || x.Email.ToLower() == email,
            cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "A staff account with this username or email already exists." });
        }

        var entity = new StaffUser
        {
            Username = username,
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            Role = string.Equals(request.Role, "staff", StringComparison.OrdinalIgnoreCase) ? "Staff" : "Admin",
            IsActive = true,
            NotificationsEnabled = true,
            CanApproveReversals = true,
            DateCreated = DateTime.UtcNow
        };

        db.StaffUsers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Account created successfully." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new { message = "Provide username/email and a new password (minimum 6 chars)." });
        }

        var lookup = request.UsernameOrEmail.Trim().ToUpperInvariant();
        var user = await db.StaffUsers.FirstOrDefaultAsync(
            x => x.Username.ToUpper() == lookup || x.Email.ToUpper() == lookup,
            cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "Account not found." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword.Trim());
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Password reset successful." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthIdentityDto>> Me(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Staff"))
        {
            var staffIdClaim = User.FindFirstValue("staff_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(staffIdClaim, out var staffId))
            {
                return Unauthorized(new { message = "Invalid admin token." });
            }

            var staff = await db.StaffUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);
            if (staff is null)
            {
                return Unauthorized(new { message = "Admin not found." });
            }

            return Ok(new AuthIdentityDto
            {
                StaffUserId = staff.Id,
                Role = NormalizeStaffRole(staff.Role),
                DisplayName = staff.Name,
                Email = staff.Email
            });
        }

        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(customerIdClaim, out var customerId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        return Ok(new AuthIdentityDto
        {
            CustomerId = customer.Id,
            Role = "User",
            DisplayName = customer.Name,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email
        });
    }

    private async Task<AuthResponseDto> CreateUserAuthResponseAsync(Customer customer, CancellationToken cancellationToken)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.TokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Role, "User"),
            new(ClaimTypes.Name, customer.Name),
            new(ClaimTypes.MobilePhone, customer.PhoneNumber),
            new(ClaimTypes.Email, customer.Email),
            new("phone_number", customer.PhoneNumber)
        };

        var token = JwtTokenFactory.CreateToken(_jwtOptions, claims, expiresAt);
        db.MobileAppSessions.Add(new MobileAppSession
        {
            CustomerId = customer.Id,
            JwtToken = token,
            Expiry = expiresAt
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            Identity = new AuthIdentityDto
            {
                CustomerId = customer.Id,
                Role = "User",
                DisplayName = customer.Name,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email
            }
        };
    }

    private static string NormalizeStaffRole(string? role)
    {
        if (string.Equals(role?.Trim(), "staff", StringComparison.OrdinalIgnoreCase))
        {
            return "Staff";
        }

        return "Admin";
    }
}


