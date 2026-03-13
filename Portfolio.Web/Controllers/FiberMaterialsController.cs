using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FiberMaterialsController : ControllerBase
{
    private readonly IFiberMaterialService _materialService;
    public FiberMaterialsController(IFiberMaterialService materialService)
    {
        _materialService = materialService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _materialService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveStock(int id, [FromBody] ReceiveStockDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _materialService.ReceiveStockAsync(id, dto, cancellationToken);
        return Ok(result);
    }
}
