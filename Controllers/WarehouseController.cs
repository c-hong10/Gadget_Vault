using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace GadgetVault.Controllers
{
    [Authorize(Roles = "Admin, SystemManager, WarehouseManager, WarehouseStaff, SalesAndProcurement")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GadgetVault.Services.IShippingService _shippingService;

        public WarehouseController(ApplicationDbContext context, GadgetVault.Services.IShippingService shippingService)
        {
            _context = context;
            _shippingService = shippingService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, WarehouseStaff, SalesAndProcurement")]
        public async Task<IActionResult> LiveInventory()
        {
            var inventory = await _context.StockLevels
                .Include(s => s.Product)
                    .ThenInclude(p => p!.Category)
                .Include(s => s.Location)
                .Where(s => s.Product != null && s.Product.IsActive && s.Location != null)
                .OrderBy(s => s.Product!.Name)
                .ThenBy(s => s.Location!.Zone)
                .ToListAsync();

            return View(inventory);
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
                if (po.Status != POStatus.Acknowledged && po.Status != POStatus.Shipped)
                {
                    TempData["Error"] = "Only Acknowledged or Shipped orders can be received.";
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

                // 4. Finalize PO Status
                po.Status = POStatus.Received;
                await _context.SaveChangesAsync();

                // Record STOCK_RECEIVED event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "STOCK_RECEIVED",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = System.DateTime.UtcNow,
                    Details = $"Received stock for PO: {po.PONumber}. Total items added: {totalItemsAdded} across {po.Items.Count} lines."
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                TempData["Success"] = $"Successfully received PO {po.PONumber}. Stock updated in {locationId}.";
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
        public async Task<IActionResult> DownloadShipmentLabel(int id)
        {
            var order = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            var brandName = settings?.CompanyName ?? "GadgetVault";
            var brandColor = settings?.PrimaryColorHex ?? "#23a476";

            var itemsHtml = string.Join("", order.Items.Select(i =>
                $"<tr><td style='padding:4px 0;font-size:11px;color:#333;'>{i.Product?.Name ?? "—"}</td>" +
                $"<td style='padding:4px 0;font-size:11px;color:#333;text-align:center;'>{i.Quantity}</td></tr>"));

            var barcodeUrl = $"https://barcode.tec-it.com/barcode.ashx?data={Uri.EscapeDataString(order.SONumber)}&code=Code128&dpi=96&unit=Min&imagetype=Png&rotation=0&color=%23000000&bgcolor=%23ffffff";

            var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<title>Label_{order.SONumber}</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  @page {{ size: 4in 6in; margin: 0; }}
  body {{ width: 4in; height: 6in; font-family: 'Arial', sans-serif; background: #fff; }}
  .label {{ width: 100%; height: 100%; border: 2px solid #222; display: flex; flex-direction: column; }}
  .header {{ background: {brandColor}; color: #fff; padding: 10px 12px; display: flex; align-items: center; justify-content: space-between; }}
  .header-brand {{ font-size: 15px; font-weight: 900; letter-spacing: -0.5px; }}
  .header-badge {{ font-size: 9px; font-weight: 700; opacity: 0.85; text-transform: uppercase; letter-spacing: 1px; }}
  .section {{ padding: 8px 12px; border-bottom: 1px solid #eee; }}
  .label-xs {{ font-size: 8px; font-weight: 700; text-transform: uppercase; color: #888; letter-spacing: 0.5px; margin-bottom: 2px; }}
  .label-val {{ font-size: 13px; font-weight: 700; color: #111; }}
  .label-val-sm {{ font-size: 11px; font-weight: 600; color: #333; }}
  .barcode-section {{ padding: 8px 12px; text-align: center; }}
  .barcode-section img {{ max-width: 100%; height: 50px; }}
  .barcode-text {{ font-size: 10px; font-family: monospace; font-weight: 700; color: #333; margin-top: 3px; }}
  table {{ width: 100%; border-collapse: collapse; }}
  th {{ font-size: 8px; font-weight: 700; text-transform: uppercase; color: #888; padding: 2px 0; border-bottom: 1px solid #eee; }}
  .footer {{ margin-top: auto; padding: 6px 12px; background: #f8f8f8; border-top: 1px solid #eee; text-align: center; }}
  .footer p {{ font-size: 8px; color: #aaa; }}
</style>
</head>
<body>
<div class='label'>
  <div class='header'>
    <span class='header-brand'>📦 {brandName}</span>
    <span class='header-badge'>Shipment Label</span>
  </div>
  <div class='section'>
    <div class='label-xs'>Ship To</div>
    <div class='label-val'>{order.Customer?.CompanyName ?? "—"}</div>
    <div class='label-val-sm'>{order.Customer?.Address ?? ""}</div>
  </div>
  <div class='section' style='display:grid;grid-template-columns:1fr 1fr;gap:8px;'>
    <div>
      <div class='label-xs'>Order #</div>
      <div class='label-val-sm' style='font-family:monospace;'>{order.SONumber}</div>
    </div>
    <div>
      <div class='label-xs'>Date</div>
      <div class='label-val-sm'>{order.OrderDate:MMM dd, yyyy}</div>
    </div>
    <div>
      <div class='label-xs'>Tracking #</div>
      <div class='label-val-sm' style='font-family:monospace;color:#23a476;'>{order.TrackingNumber ?? "N/A"}</div>
    </div>
    <div>
      <div class='label-xs'>Items</div>
      <div class='label-val-sm'>{order.Items.Count} line item(s)</div>
    </div>
  </div>
  <div class='section'>
    <div class='label-xs' style='margin-bottom:4px;'>Package Contents</div>
    <table>
      <thead><tr><th style='text-align:left;'>Product</th><th>Qty</th></tr></thead>
      <tbody>{itemsHtml}</tbody>
    </table>
  </div>
  <div class='barcode-section'>
    <img src='{barcodeUrl}' alt='Barcode for {order.SONumber}' onerror=""this.style.display='none'"" />
    <div class='barcode-text'>{order.SONumber}</div>
  </div>
  <div class='footer'><p>Verified Shipment Archive · {brandName} WMS · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p></div>
</div>
</body></html>";

            return Content(html, "text/html", System.Text.Encoding.UTF8);
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

            // 2. Generate Shipping Label (Simulated Shipping API)
            var (trackingNumber, labelUrl) = await _shippingService.GenerateShippingLabelAsync(order.SONumber, order.Customer?.CompanyName ?? "Customer");

            // 3. Update Order Status & Tracking Info
            order.Status = SOStatus.Shipped;
            order.TrackingNumber = trackingNumber;
            order.ShippingLabelUrl = labelUrl;
            
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order {order.SONumber} successfully dispatched! Tracking Number: {trackingNumber}";
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
        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmptyStockRecord(int inventoryId)
        {
            var record = await _context.StockLevels
                .Include(s => s.Product)
                .Include(s => s.Location)
                .FirstOrDefaultAsync(s => s.Id == inventoryId);

            if (record == null)
            {
                return Json(new { success = false, message = "Inventory record not found." });
            }

            if (record.Quantity > 0)
            {
                return Json(new { success = false, message = $"Cannot delete record for {record.Product?.Name} because it still has {record.Quantity} units in {record.Location?.Zone}-{record.Location?.Aisle}." });
            }

            _context.StockLevels.Remove(record);
            await _context.SaveChangesAsync();

            // Record the cleanup in audit logs
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "INVENTORY_CLEANUP",
                PerformedBy = User.Identity?.Name ?? "Admin",
                Timestamp = System.DateTime.UtcNow,
                Details = $"Deleted empty stock record for {record.Product?.Name} (ID: {record.ProductId}) at location {record.Location?.Zone}-{record.Location?.Aisle} (ID: {record.LocationId})."
            });
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Empty location record successfully removed from inventory." });
        }
    }
}
