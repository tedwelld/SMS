using Microsoft.AspNetCore.Mvc;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/access-methods")]
public class AccessMethodsController(IAccessMethodService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccessMethodDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccessMethodDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<AccessMethodDto>> Create([FromBody] CreateAccessMethodRequest request, CancellationToken cancellationToken)
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


