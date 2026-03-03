using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/staff-users")]
public class StaffUsersController(IStaffUserService service, SmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffUserDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffUserDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<StaffUserDto>> Create([FromBody] CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username, name, email, and password are required." });
        }

        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ? 409 : 400;
            return StatusCode(status, new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "A user with this username or email already exists." });
        }
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<object>> UpdateStatus(int id, [FromBody] UpdateStaffStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.StaffUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var normalized = request.Status.Trim().ToLowerInvariant();
        if (normalized is not ("active" or "inactive" or "suspended"))
        {
            return BadRequest(new { message = "Status must be active, inactive, or suspended." });
        }

        entity.IsActive = normalized == "active";
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<object>> UpdateRole(int id, [FromBody] UpdateStaffRoleRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.StaffUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new { message = "Role is required." });
        }

        var normalized = request.Role.Trim().ToLowerInvariant();
        entity.Role = normalized switch
        {
            "staff" => "Staff",
            "manager" => "Manager",
            _ => "Admin"
        };

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    public sealed class UpdateStaffStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public sealed class UpdateStaffRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}


