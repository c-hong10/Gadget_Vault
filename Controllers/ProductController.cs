using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GadgetVault.Controllers
{
    [Authorize(Roles = "Admin, SystemManager, WarehouseManager, SalesAndProcurement, WarehouseStaff, Supplier")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GadgetVault.Services.ImageService _imageService;

        public ProductController(ApplicationDbContext context, GadgetVault.Services.ImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        // GET /Product/Index — used internally; ProductCatalog view calls via DashboardController
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .OrderBy(p => p.Name)
                .ToListAsync();
            return View(products);
        }

        // GET /Product/Edit/{id}
        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // Isolation for suppliers
            if (User.IsInRole("Supplier"))
            {
                var username = User.Identity?.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null || product.SupplierId != user.SupplierId) return Unauthorized();
            }

            ViewBag.Categories = new SelectList(_context.Categories.ToList(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST /Product/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile? imageFile)
        {
            if (id != model.Id) 
            {
                TempData["Error"] = "Invalid product ID.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = "Validation failed: " + errors;
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            var existing = await _context.Products.FindAsync(id);
            if (existing == null) 
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            // Isolation for suppliers
            if (User.IsInRole("Supplier"))
            {
                var username = User.Identity?.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null || existing.SupplierId != user.SupplierId) return Unauthorized();
            }

            // Duplicate SKU guard
            if (await _context.Products.AnyAsync(p => p.SKU == model.SKU.Trim() && p.Id != id))
            {
                TempData["Error"] = $"This SKU \"{model.SKU.Trim()}\" is already assigned to another gadget.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            // Duplicate Barcode guard
            if (!string.IsNullOrWhiteSpace(model.Barcode) && await _context.Products.AnyAsync(p => p.Barcode == model.Barcode.Trim() && p.Id != id))
            {
                TempData["Error"] = $"This Barcode \"{model.Barcode.Trim()}\" is already assigned to another gadget.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            try
            {
                if (imageFile != null)
                {
                    var url = await _imageService.UploadImageAsync(imageFile);
                    if (!string.IsNullOrEmpty(url)) existing.ImageUrl = url;
                }
                existing.Name = model.Name.Trim();
                existing.SKU = model.SKU.Trim();
                
                // Pricing Logic
                existing.CostPrice = model.CostPrice;
                if (!User.IsInRole("Supplier"))
                {
                    existing.SellingPrice = model.SellingPrice;
                    existing.SupplierId = model.SupplierId;
                }

                existing.CategoryId = model.CategoryId;
                existing.Description = model.Description?.Trim();
                existing.Barcode = model.Barcode?.Trim();

                await _context.SaveChangesAsync();

                // Record PRODUCT_UPDATED event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "PRODUCT_UPDATED",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Updated product: {existing.Name} (SKU: {existing.SKU})"
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Product updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "Unable to save changes. Please try again.";
            }
            
            // Redirect back to correct catalog
            if (User.IsInRole("Supplier")) return RedirectToAction("SupplierCatalog", "Dashboard");
            return RedirectToAction("ProductCatalog", "Dashboard");
        }

        // POST /Product/Archive/{id}
        [HttpPost]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> Archive(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return Json(new { success = false, message = "Product not found." });

            // Isolation for suppliers
            if (User.IsInRole("Supplier"))
            {
                var username = User.Identity?.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null || product.SupplierId != user.SupplierId)
                {
                    return Json(new { success = false, message = "Access denied." });
                }
            }

            product.IsActive = false;
            
            // Record PRODUCT_DELETED event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRODUCT_DELETED",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Archived product: {product.Name} (SKU: {product.SKU})"
            });
            
            await _context.SaveChangesAsync();
            TempData["Success"] = $"\"{product.Name}\" has been moved to the archive vault.";
            return Json(new { success = true, message = "Product archived." });
        }

        // POST /Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> Create(Product model, IFormFile? imageFile)
        {
            // Data Isolation & Default Pricing
            if (User.IsInRole("Supplier"))
            {
                var username = User.Identity?.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null || user.SupplierId == null) return Unauthorized();
                
                model.SupplierId = user.SupplierId.Value;
                // Suppliers can't set selling price, force 20% markup
                model.SellingPrice = model.CostPrice * 1.2m; 
            }
            else if (model.SellingPrice == 0)
            {
                // Auto-markup if not provided
                model.SellingPrice = model.CostPrice * 1.2m;
            }

            // Re-validate since we might have changed SupplierId or SellingPrice
            ModelState.Clear();
            TryValidateModel(model);

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = "Validation failed: " + errors;
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            // Duplicate SKU guard
            if (await _context.Products.AnyAsync(p => p.SKU == model.SKU.Trim()))
            {
                TempData["Error"] = $"This SKU \"{model.SKU.Trim()}\" is already assigned to another gadget.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            string? uploadedImageUrl = null;
            if (imageFile != null)
            {
                uploadedImageUrl = await _imageService.UploadImageAsync(imageFile);
            }

            var newProduct = new Product
            {
                Name         = model.Name.Trim(),
                SKU          = model.SKU.Trim(),
                Barcode      = model.Barcode?.Trim(),
                CostPrice    = model.CostPrice,
                SellingPrice = model.SellingPrice,
                Description  = model.Description?.Trim(),
                ImageUrl     = uploadedImageUrl,
                CategoryId   = model.CategoryId,
                SupplierId   = model.SupplierId
            };

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();

            // Record PRODUCT_CREATED event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PRODUCT_CREATED",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Created product: {newProduct.Name} (SKU: {newProduct.SKU})"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product \"{newProduct.Name}\" added to the catalog.";

            // Redirect back to the correct catalog view
            if (User.IsInRole("Supplier")) return RedirectToAction("SupplierCatalog", "Dashboard");
            return RedirectToAction("ProductCatalog", "Dashboard");
        }
    }
}
