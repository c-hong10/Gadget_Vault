using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;

namespace GadgetVault.Controllers
{
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Seed default categories if the table is empty
        private async Task EnsureSeedDataAsync()
        {
            if (!await _context.Categories.AnyAsync())
            {
                _context.Categories.AddRange(
                    new Category { Name = "Enterprise Networking",  Description = "Switches, routers, access points, and enterprise LAN/WAN hardware." },
                    new Category { Name = "HPC Hardware",           Description = "High-performance compute cards, workstation CPUs, and server-grade memory." },
                    new Category { Name = "Workspace Solutions",    Description = "Monitors, docking stations, ergonomic peripherals, and collaboration tools." }
                );
                await _context.SaveChangesAsync();
            }
        }

        // GET /Category/Index — used internally; MasterData view calls this via DashboardController
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await EnsureSeedDataAsync();
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        // POST /Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            _context.Categories.Add(new Category
            {
                Name        = name.Trim(),
                Description = description?.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Category \"{name.Trim()}\" created successfully.";
            return RedirectToAction("MasterData", "Dashboard");
        }

        // POST /Category/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, string? description)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            category.Name = name.Trim();
            category.Description = description?.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Category \"{name.Trim()}\" updated successfully.";
            return RedirectToAction("MasterData", "Dashboard");
        }

        // POST /Category/Archive/{id}
        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "Category not found." });
            }

            category.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Category \"{category.Name}\" archived successfully.";
            return Json(new { success = true });
        }

        // GET /Category/GetArchived
        [HttpGet]
        public async Task<IActionResult> GetArchived()
        {
            var archived = await _context.Categories
                .Where(c => !c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new {
                    c.Id,
                    c.Name,
                    c.Description,
                    ProductCount = c.Products.Count
                })
                .ToListAsync();
            return Json(archived);
        }

        // POST /Category/Restore/{id}
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "Category not found." });
            }

            category.IsActive = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Category \"{category.Name}\" restored successfully.";
            return Json(new { success = true });
        }
    }
}
