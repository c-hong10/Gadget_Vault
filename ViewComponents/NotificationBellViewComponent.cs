using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GadgetVault.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationBellViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync() ?? new SystemSettings();
            int globalThreshold = settings.LowStockThreshold;

            var lowStockItems = await _context.StockLevels
                .Include(s => s.Product)
                .Include(s => s.Location)
                .Where(s => s.Product != null && s.Product.IsActive && s.Quantity <= globalThreshold)
                .Select(s => new LowStockNotification {
                    ProductName = s.Product.Name,
                    SKU = s.Product.SKU,
                    CurrentQty = s.Quantity,
                    Threshold = globalThreshold,
                    LocationLabel = s.Location != null ? $"{s.Location.Zone}-{s.Location.Aisle}" : "Unknown",
                    StatusLabel = s.Quantity == 0 ? "No Stock" : "Low Stock"
                })
                .ToListAsync();

            return View(lowStockItems);
        }
    }

    public class LowStockNotification
    {
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int CurrentQty { get; set; }
        public int Threshold { get; set; }
        public string LocationLabel { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
    }
}
