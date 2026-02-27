using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    /// <summary>
    /// API endpoints for managing collections.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "Authenticated")]
    public class CollectionsController : ControllerBase
    {
        private readonly ICollectionService _service;

        public CollectionsController(ICollectionService service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets all collections for the authenticated user.
        /// </summary>
        /// <returns>List of collections.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CollectionDto>), 200)]
        public async Task<ActionResult<IEnumerable<CollectionDto>>> GetAll(CancellationToken cancellationToken)
        {
            var items = await _service.GetAllAsync(cancellationToken);
            return Ok(items);
        }

        /// <summary>
        /// Gets a collection by its ID.
        /// </summary>
        /// <param name="id">Collection ID.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The collection.</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CollectionDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<CollectionDto>> Get(int id, CancellationToken cancellationToken)
        {
            var item = await _service.GetByIdAsync(id, cancellationToken);
            if (item == null) return NotFound();
            return Ok(item);
        }

        /// <summary>
        /// Creates a new collection.
        /// </summary>
        /// <param name="dto">Collection data.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The created collection.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(CollectionDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<CollectionDto>> Create([FromBody] CollectionCreateDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var created = await _service.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing collection.
        /// </summary>
        /// <param name="id">Collection ID.</param>
        /// <param name="dto">Update data.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The updated collection.</returns>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(CollectionDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] CollectionUpdateDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, cancellationToken);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Deletes a collection by its ID.
        /// </summary>
        /// <param name="id">Collection ID.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>No content if deleted, NotFound if not found.</returns>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var deleted = await _service.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}