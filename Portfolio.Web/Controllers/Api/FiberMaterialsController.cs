using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for managing fiber materials inventory (CRUD and stock receipts)
    /// scoped to the authenticated user.
    /// </summary>
    [Authorize(Policy = "Authenticated")]
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/fiber/materials")]
    public class FiberMaterialsController : ControllerBase
    {
    private readonly IFiberMaterialService _materialService;
    private readonly ILogger<FiberMaterialsController> _logger;

    public FiberMaterialsController(IFiberMaterialService materialService, ILogger<FiberMaterialsController> logger)
    {
        _materialService = materialService;
        _logger = logger;
    }

    // Maps a create/update request DTO to the internal material DTO (server-computed fields default).
    // Accepts UpdateFiberMaterialDto too, since it derives from CreateFiberMaterialDto.
    private static FiberMaterialDto ToDto(CreateFiberMaterialDto dto) => new()
    {
        Name = dto.Name,
        Sku = dto.Sku,
        Category = dto.Category,
        QtyOnHand = dto.QtyOnHand,
        UnitCost = dto.UnitCost,
        ReorderPoint = dto.ReorderPoint
    };

    /// <summary>Retrieves all fiber materials.</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.GetAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(GetAll));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Retrieves a fiber material by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.GetByIdAsync(id, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(Get));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Creates a new fiber material.</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create([FromBody] CreateFiberMaterialDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var result = await _materialService.CreateAsync(ToDto(dto), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(Create));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Updates an existing fiber material.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFiberMaterialDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var result = await _materialService.UpdateAsync(id, ToDto(dto), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(Update));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Deletes a fiber material by ID.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _materialService.DeleteAsync(id, cancellationToken);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(Delete));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Records a stock receipt for a fiber material.</summary>
    [HttpPost("{id}/receive")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ReceiveStock(int id, [FromBody] ReceiveStockDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var result = await _materialService.ReceiveStockAsync(id, dto, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberMaterialsController), nameof(ReceiveStock));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    }
}
