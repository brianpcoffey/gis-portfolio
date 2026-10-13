using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>API endpoints for managing fiber orders (CRUD) scoped to the authenticated user.</summary>
    [Authorize(Policy = "Authenticated")]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/fiber/orders")]
    public class FiberOrdersController : ControllerBase
    {
    private readonly IFiberOrderService _orderService;

    public FiberOrdersController(IFiberOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>Retrieves all fiber orders.</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _orderService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves a fiber order by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Creates a new fiber order.</summary>
    [HttpPost]
    [RequestSizeLimit(65_536)]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create([FromBody] CreateFiberOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _orderService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Updates an existing fiber order.</summary>
    [HttpPut("{id}")]
    [RequestSizeLimit(65_536)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFiberOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _orderService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a fiber order by ID.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _orderService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    }
}
