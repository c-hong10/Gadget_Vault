using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace GadgetVault.Controllers
{
    [Authorize(Roles = "SystemManager, WarehouseManager, WarehouseStaff")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehouseController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceipt(int poId, int locationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Fetch the Purchase Order
                var po = await _context.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == poId);

                if (po == null) return NotFound();
                if (po.Status != POStatus.Acknowledged)
                {
                    TempData["Error"] = "Only Acknowledged orders can be received.";
                    return RedirectToAction("ReceiveStock", "Dashboard");
                }

                // 2. Process Items and Update Stock
                int totalItemsAdded = 0;
                foreach (var item in po.Items)
                {
                    // Update Stock Balance
                    var stock = await _context.StockLevels
                        .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.LocationId == locationId);

                    if (stock == null)
                    {
                        stock = new StockLevel
                        {
                            ProductId = item.ProductId,
                            LocationId = locationId,
                            Quantity = item.Quantity,
                            LastUpdated = System.DateTime.UtcNow
                        };
                        _context.StockLevels.Add(stock);
                    }
                    else
                    {
                        stock.Quantity += item.Quantity;
                        stock.LastUpdated = System.DateTime.UtcNow;
                    }

                    // Create Ledger Record
                    var ledger = new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Type = TransactionType.StockIn,
                        ReferenceId = po.PONumber,
                        PurchaseOrderId = poId,
                        LocationId = locationId,
                        Timestamp = System.DateTime.UtcNow,
                        Notes = $"Received via PO: {po.PONumber}"
                    };
                    _context.InventoryTransactions.Add(ledger);
                    
                    totalItemsAdded += item.Quantity;
                }

                // 3. Finalize PO Status
                po.Status = POStatus.Received;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Successfully received PO {po.PONumber}. {totalItemsAdded} items added to stock.";
                return RedirectToAction("ReceiveStock", "Dashboard");
            }
            catch (System.Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Critical error during inventory injection. Transaction rolled back.";
                return RedirectToAction("ReceiveStock", "Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogManualShipment(int productId, int quantity, int locationId, int supplierId)
        {
            if (productId <= 0 || quantity <= 0 || locationId <= 0 || supplierId <= 0)
            {
                TempData["Error"] = "Invalid shipment details. All fields are required.";
                return RedirectToAction("ReceiveStock", "Dashboard");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products.FindAsync(productId);
                var location = await _context.WarehouseLocations.FindAsync(locationId);
                var supplier = await _context.BusinessPartners.FindAsync(supplierId);

                if (product == null || location == null || supplier == null)
                {
                    TempData["Error"] = "Validation failed: Product, Location, or Supplier not found in database.";
                    return RedirectToAction("ReceiveStock", "Dashboard");
                }

                // 1. Update Stock Level
                var stock = await _context.StockLevels
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.LocationId == locationId);

                if (stock == null)
                {
                    stock = new StockLevel
                    {
                        ProductId = productId,
                        LocationId = locationId,
                        Quantity = quantity,
                        LastUpdated = System.DateTime.UtcNow
                    };
                    _context.StockLevels.Add(stock);
                }
                else
                {
                    stock.Quantity += quantity;
                    stock.LastUpdated = System.DateTime.UtcNow;
                }

                // 2. Create Ledger Record
                var ledger = new InventoryTransaction
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Type = TransactionType.StockIn,
                    ReferenceId = $"MAN-{System.DateTime.Now:yyyyMMdd}-{productId}",
                    LocationId = locationId,
                    Timestamp = System.DateTime.UtcNow,
                    Notes = $"Manual log from {supplier.CompanyName}"
                };
                _context.InventoryTransactions.Add(ledger);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Successfully logged {quantity} units of {product.Name} from {supplier.CompanyName}.";
                return RedirectToAction("ReceiveStock", "Dashboard");
            }
            catch (System.Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Critical error during manual logging. Transaction rolled back.";
                return RedirectToAction("ReceiveStock", "Dashboard");
            }
        }
        [HttpGet]
        public async Task<IActionResult> Fulfillment()
        {
            var activeOrders = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.Status == SOStatus.Picking || o.Status == SOStatus.Packed)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            var shippedOrders = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .Where(o => o.Status == SOStatus.Shipped)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentShipments = shippedOrders;
            return View(activeOrders);
        }

        [HttpGet]
        public async Task<IActionResult> GetPickList(int id)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Fetch locations for all products in the order
            var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
            var stockLevels = await _context.StockLevels
                .Include(s => s.Location)
                .Where(s => productIds.Contains(s.ProductId) && s.Quantity > 0)
                .ToListAsync();

            ViewBag.StockLevels = stockLevels;
            return PartialView("_PickList", order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPacked(int id)
        {
            var order = await _context.SalesOrders.FindAsync(id);
            if (order == null || order.Status != SOStatus.Picking)
            {
                TempData["Error"] = "Order not found or not in Picking status.";
                return RedirectToAction("Fulfillment");
            }

            order.Status = SOStatus.Packed;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order {order.SONumber} marked as Packed and ready for dispatch.";
            return RedirectToAction("Fulfillment");
        }

        [HttpGet]
        public async Task<IActionResult> GetShipmentDetails(int id)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Fetch the dispatch transactions to get locations and timestamp
            var transactions = await _context.InventoryTransactions
                .Include(t => t.Location)
                .Where(t => t.ReferenceId == order.SONumber && t.Type == TransactionType.StockOut)
                .ToListAsync();

            ViewBag.Transactions = transactions;
            return PartialView("_ShipmentDetails", order);
        }

        [HttpGet]
        public async Task<IActionResult> PickList(int id)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Fetch locations for all products in the order
            var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
            var stockLevels = await _context.StockLevels
                .Include(s => s.Location)
                .Where(s => productIds.Contains(s.ProductId) && s.Quantity > 0)
                .ToListAsync();

            ViewBag.StockLevels = stockLevels;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatch(int id)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != SOStatus.Packed)
            {
                TempData["Error"] = "Only packed orders can be dispatched.";
                return RedirectToAction("Fulfillment");
            }

            // 1. Deduct Stock and Log Transactions
            foreach (var item in order.Items)
            {
                var stockRecords = await _context.StockLevels
                    .Where(s => s.ProductId == item.ProductId && s.Quantity > 0)
                    .OrderByDescending(s => s.Quantity)
                    .ToListAsync();

                int remainingToDeduct = item.Quantity;
                
                // If total stock is insufficient, we still deduct (allowing negative stock is rare but possible in some ERPs, 
                // but here we'll just deduct what we can and log it).
                // Actually, let's be strict.
                int totalAvailable = stockRecords.Sum(s => s.Quantity);
                if (totalAvailable < item.Quantity)
                {
                    TempData["Error"] = $"Insufficient stock for Product ID {item.ProductId}. Available: {totalAvailable}, Required: {item.Quantity}";
                    return RedirectToAction("Fulfillment");
                }

                foreach (var stock in stockRecords)
                {
                    if (remainingToDeduct <= 0) break;

                    int deductAmount = Math.Min(stock.Quantity, remainingToDeduct);
                    stock.Quantity -= deductAmount;
                    remainingToDeduct -= deductAmount;

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        Quantity = -deductAmount,
                        Type = TransactionType.StockOut,
                        Timestamp = DateTime.UtcNow,
                        Notes = $"Sales Order {order.SONumber} Dispatched",
                        LocationId = stock.LocationId,
                        ReferenceId = order.SONumber
                    });
                }
            }

            // 2. Update Order Status
            order.Status = SOStatus.Shipped;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order {order.SONumber} dispatched. Inventory levels updated.";
            return RedirectToAction("Fulfillment");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, SOStatus newStatus)
        {
            var order = await _context.SalesOrders.FindAsync(id);
            if (order == null) return NotFound();

            // Simple validation: only allow moving forward
            if (order.Status == SOStatus.Pending && newStatus == SOStatus.Picking)
            {
                order.Status = SOStatus.Picking;
            }
            else if (order.Status == SOStatus.Picking && newStatus == SOStatus.Packed)
            {
                order.Status = SOStatus.Packed;
            }
            else
            {
                TempData["Error"] = "Invalid status transition.";
                return RedirectToAction("Fulfillment");
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Order {order.SONumber} status updated to {newStatus}.";
            return RedirectToAction("Fulfillment");
        }
    }
}
