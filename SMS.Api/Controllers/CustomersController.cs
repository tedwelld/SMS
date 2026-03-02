using Microsoft.AspNetCore.Mvc;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(ICustomerService service) : ControllerBase
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}


