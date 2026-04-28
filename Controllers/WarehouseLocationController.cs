using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;

namespace GadgetVault.Controllers
{
    public class WarehouseLocationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehouseLocationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string zone, string aisle, string rack)
        {
            if (string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(aisle))
            {
                TempData["Error"] = "Zone Code and Zone Name are required.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            _context.WarehouseLocations.Add(new WarehouseLocation
            {
                Zone = zone.Trim(),
                Aisle = aisle.Trim(),
                Rack = rack?.Trim() ?? string.Empty,
                Bin = string.Empty // Unused for now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Zone \"{zone.Trim()}\" created successfully.";
            return RedirectToAction("MasterData", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string zone, string aisle, string rack)
        {
            var location = await _context.WarehouseLocations.FindAsync(id);
            if (location == null)
            {
                TempData["Error"] = "Zone not found.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            if (string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(aisle))
            {
                TempData["Error"] = "Zone Code and Zone Name are required.";
                return RedirectToAction("MasterData", "Dashboard");
            }

            location.Zone = zone.Trim();
            location.Aisle = aisle.Trim();
            location.Rack = rack?.Trim() ?? string.Empty;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Zone \"{zone.Trim()}\" updated successfully.";
            return RedirectToAction("MasterData", "Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var location = await _context.WarehouseLocations.FindAsync(id);
            if (location == null)
            {
                return Json(new { success = false, message = "Zone not found." });
            }

            _context.WarehouseLocations.Remove(location);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Zone \"{location.Zone}\" archived successfully.";
            return Json(new { success = true });
        }
    }
}
