using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Models;
using GadgetVault.Data;

namespace GadgetVault.Controllers
{
    [Route("api/stocklevels")]
    [ApiController]
    public class StockLevelsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StockLevelsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/stocklevels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StockLevel>>> GetStockLevels()
        {
            return await _context.StockLevels.Include(s => s.Product).ToListAsync();
        }

        // GET: api/stocklevels/5
        [HttpGet("{id}")]
        public async Task<ActionResult<StockLevel>> GetStockLevel(int id)
        {
            var stockLevel = await _context.StockLevels.Include(s => s.Product).FirstOrDefaultAsync(s => s.Id == id);

            if (stockLevel == null)
            {
                return NotFound();
            }

            return stockLevel;
        }

        // POST: api/stocklevels
        [HttpPost]
        public async Task<ActionResult<StockLevel>> PostStockLevel(StockLevel stockLevel)
        {
            stockLevel.LastUpdated = DateTime.UtcNow;
            _context.StockLevels.Add(stockLevel);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStockLevel), new { id = stockLevel.Id }, stockLevel);
        }

        // PUT: api/stocklevels/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutStockLevel(int id, StockLevel stockLevel)
        {
            if (id != stockLevel.Id)
            {
                return BadRequest();
            }

            stockLevel.LastUpdated = DateTime.UtcNow;
            _context.Entry(stockLevel).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockLevelExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        private bool StockLevelExists(int id)
        {
            return _context.StockLevels.Any(e => e.Id == id);
        }
    }
}
