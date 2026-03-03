using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(ICustomerService service, SmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        var normalizedPhone = string.Concat((request.PhoneNumber ?? string.Empty).Trim().Where(ch => char.IsDigit(ch) || ch == '+'));
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return BadRequest(new { message = "Valid phone number is required." });
        }

        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            normalizedEmail = $"{normalizedPhone}@tarwallet.local";
        }

        var normalizedName = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var phoneExists = await db.Customers.AnyAsync(
            x => x.Id != id && x.PhoneNumber == normalizedPhone,
            cancellationToken);
        if (phoneExists)
        {
            return BadRequest(new { message = "Phone number already exists." });
        }

        var emailExists = await db.Customers.AnyAsync(
            x => x.Id != id && x.Email == normalizedEmail,
            cancellationToken);
        if (emailExists)
        {
            return BadRequest(new { message = "Email already exists." });
        }

        customer.PhoneNumber = normalizedPhone;
        customer.Email = normalizedEmail;
        customer.Name = normalizedName;
        customer.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new CustomerDto(customer.Id, customer.PhoneNumber, customer.Email, customer.Name, customer.IsActive, customer.DateCreated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    public sealed class UpdateCustomerRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}


