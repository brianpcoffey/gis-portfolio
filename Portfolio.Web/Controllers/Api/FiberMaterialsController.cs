using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api;

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
        try
        {
            var result = await _materialService.GetAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveStock(int id, [FromBody] ReceiveStockDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _materialService.ReceiveStockAsync(id, dto, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}
