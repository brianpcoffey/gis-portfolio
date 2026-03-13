using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FiberShipmentsController : ControllerBase
{
    private readonly IFiberShipmentService _shipmentService;
    public FiberShipmentsController(IFiberShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _shipmentService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateShipmentStatusDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _shipmentService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }
}
