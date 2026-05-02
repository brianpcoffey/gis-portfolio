using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api;

[Authorize(Policy = "Authenticated")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fiber/orders")]
public class FiberOrdersController : ControllerBase
{
    private readonly IFiberOrderService _orderService;
    private readonly ILogger<FiberOrdersController> _logger;

    public FiberOrdersController(IFiberOrderService orderService, ILogger<FiberOrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>Retrieves all fiber orders.</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orderService.GetAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberOrdersController), nameof(GetAll));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Retrieves a fiber order by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orderService.GetByIdAsync(id, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberOrdersController), nameof(Get));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Creates a new fiber order.</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create([FromBody] CreateFiberOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var result = await _orderService.CreateAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberOrdersController), nameof(Create));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Updates an existing fiber order.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFiberOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var result = await _orderService.UpdateAsync(id, dto, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberOrdersController), nameof(Update));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }

    /// <summary>Deletes a fiber order by ID.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _orderService.DeleteAsync(id, cancellationToken);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in {Controller}.{Action}.",
                nameof(FiberOrdersController), nameof(Delete));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title  = "An unexpected error occurred.",
                Detail = "See server logs for details."
            });
        }
    }
}
