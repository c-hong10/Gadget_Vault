using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GadgetVault.Data;
using GadgetVault.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GadgetVault.Controllers
{
    [Authorize(Roles = "SystemManager, WarehouseManager, SalesAndProcurement")]
    public class SalesOrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesOrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? status)
        {
            var query = _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(o => o.SONumber.ToLower().Contains(s) || 
                                        (o.Customer != null && o.Customer.CompanyName.ToLower().Contains(s)));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SOStatus>(status, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            // Populate view data for the creation modal
            // CRITICAL LINK: Exclusive to Customers
            ViewBag.Customers = await _context.BusinessPartners
                .Where(p => p.IsActive && p.PartnerType == PartnerType.Customer)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            // Products with stock quantities
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var stockMap = await _context.StockLevels
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.TotalQty);

            ViewBag.Products = products;
            ViewBag.StockMap = stockMap;
            ViewBag.TotalCount = await _context.SalesOrders.CountAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int customerId, DateTime orderDate, string? notes, List<SalesOrderItem> Items)
        {
            if (customerId <= 0)
            {
                TempData["Error"] = "Please select a valid customer.";
                return RedirectToAction("Index");
            }

            // Auto-generate SO Number: SO-YYYYMMDD-COUNT+1
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await _context.SalesOrders
                .CountAsync(o => o.SONumber.Contains(dateStr));
            var soNumber = $"SO-{dateStr}-{(countToday + 1).ToString("D3")}";

            // Hard Validation: Stock Availability
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) continue;

                    var totalStock = await _context.StockLevels
                        .Where(s => s.ProductId == item.ProductId)
                        .SumAsync(s => (int?)s.Quantity) ?? 0;

                    var reserved = await _context.SalesOrderItems
                        .Where(i => i.ProductId == item.ProductId && 
                                   (i.SalesOrder.Status == SOStatus.Draft || 
                                    i.SalesOrder.Status == SOStatus.Pending || 
                                    i.SalesOrder.Status == SOStatus.Picking || 
                                    i.SalesOrder.Status == SOStatus.Packed))
                        .SumAsync(i => (int?)i.Quantity) ?? 0;

                    var available = totalStock - reserved;

                    if (item.Quantity > available)
                    {
                        TempData["Error"] = $"Cannot save draft. Item {product.Name} exceeds available warehouse stock.";
                        return RedirectToAction("Index");
                    }
                }
            }

            // Calculate Total from items
            decimal totalAmount = 0;
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    totalAmount += item.Quantity * item.UnitPrice;
                }
            }

            var so = new SalesOrder
            {
                SONumber = soNumber,
                CustomerId = customerId,
                OrderDate = orderDate,
                Status = SOStatus.Draft,
                Notes = notes,
                TotalAmount = totalAmount,
                Items = Items ?? new List<SalesOrderItem>()
            };

            _context.SalesOrders.Add(so);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sales Order {soNumber} created successfully.";
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int customerId, DateTime orderDate, string? notes, List<SalesOrderItem> Items)
        {
            var so = await _context.SalesOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (so == null || so.Status != SOStatus.Draft)
            {
                TempData["Error"] = "Order not found or cannot be edited.";
                return RedirectToAction("Index");
            }

            // Hard Validation: Stock Availability (similar to Create but considering current items)
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) continue;

                    var totalStock = await _context.StockLevels
                        .Where(s => s.ProductId == item.ProductId)
                        .SumAsync(s => (int?)s.Quantity) ?? 0;

                    var reserved = await _context.SalesOrderItems
                        .Where(i => i.ProductId == item.ProductId && i.SalesOrderId != id &&
                                   (i.SalesOrder.Status == SOStatus.Draft || 
                                    i.SalesOrder.Status == SOStatus.Pending || 
                                    i.SalesOrder.Status == SOStatus.Picking || 
                                    i.SalesOrder.Status == SOStatus.Packed))
                        .SumAsync(i => (int?)i.Quantity) ?? 0;

                    var available = totalStock - reserved;

                    if (item.Quantity > available)
                    {
                        TempData["Error"] = $"Cannot update. Item {product.Name} exceeds available stock.";
                        return RedirectToAction("Index");
                    }
                }
            }

            // Update SO details
            so.CustomerId = customerId;
            so.OrderDate = orderDate;
            so.Notes = notes;

            // Update Items: Clear and re-add for simplicity in Master-Detail update
            _context.SalesOrderItems.RemoveRange(so.Items);
            so.Items = Items ?? new List<SalesOrderItem>();

            // Re-calculate Total
            so.TotalAmount = so.Items.Sum(i => i.Quantity * i.UnitPrice);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Sales Order {so.SONumber} updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            var so = await _context.SalesOrders.FindAsync(id);
            if (so == null || so.Status != SOStatus.Draft)
            {
                TempData["Error"] = "Order not found or already submitted.";
                return RedirectToAction("Index");
            }

            so.Status = SOStatus.Picking;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sales Order {so.SONumber} submitted to Picking.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var so = await _context.SalesOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (so == null) return RedirectToAction("Index");

            if (so.Status != SOStatus.Draft && so.Status != SOStatus.Picking)
            {
                TempData["Error"] = "Cannot delete orders beyond Picking status.";
                return RedirectToAction("Index");
            }

            _context.SalesOrderItems.RemoveRange(so.Items);
            _context.SalesOrders.Remove(so);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sales Order {so.SONumber} has been deleted and stock reservations released.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var so = await _context.SalesOrders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (so == null) return NotFound();

            return Json(new {
                so.Id,
                so.CustomerId,
                so.OrderDate,
                so.Notes,
                so.Status,
                Items = so.Items.Select(i => new {
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice,
                    ProductName = i.Product?.Name
                })
            });
        }
    }
}
