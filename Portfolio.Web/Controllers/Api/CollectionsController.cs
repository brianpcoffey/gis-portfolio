using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CollectionsController : ControllerBase
    {
        private readonly ICollectionService _service;

        public CollectionsController(ICollectionService service)
        {
            _service = service;
        }

        // GET: api/collections
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CollectionDto>>> GetAll(CancellationToken cancellationToken)
        {
            var items = await _service.GetAllAsync(cancellationToken);
            return Ok(items);
        }

        // GET: api/collections/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CollectionDto>> Get(int id, CancellationToken cancellationToken)
        {
            var item = await _service.GetByIdAsync(id, cancellationToken);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // POST: api/collections
        [HttpPost]
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

        // PUT: api/collections/{id}
        [HttpPut("{id:int}")]
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

        // DELETE: api/collections/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var deleted = await _service.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}