using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api;

[Authorize(Policy = "Authenticated")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fiber/shipments")]
public class FiberShipmentsController : ControllerBase
{
    private readonly IFiberShipmentService _shipmentService;
    private readonly ILogger<FiberShipmentsController> _logger;

    public FiberShipmentsController(IFiberShipmentService shipmentService, ILogger<FiberShipmentsController> logger)
    {
        _shipmentService = shipmentService;
        _logger = logger;
    }

    /// <summary>Retrieves all fiber shipments.</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _shipmentService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves a fiber shipment by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var result = await _shipmentService.GetByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Creates a new fiber shipment.</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create([FromBody] FiberShipmentDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _shipmentService.CreateAsync(dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates an existing fiber shipment.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(int id, [FromBody] FiberShipmentDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _shipmentService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates the status of a fiber shipment.</summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateShipmentStatusDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _shipmentService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a fiber shipment by ID.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _shipmentService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
