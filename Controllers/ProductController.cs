using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GadgetVault.Controllers
{
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
                .OrderBy(p => p.Name)
                .ToListAsync();
            return View(products);
        }

        // GET /Product/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = new SelectList(_context.Categories.ToList(), "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST /Product/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile? imageFile)
        {
            if (id != model.Id) 
            {
                TempData["Error"] = "Invalid product ID.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            var existing = await _context.Products.FindAsync(id);
            if (existing == null) 
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.SKU))
            {
                TempData["Error"] = "Product name and SKU are required.";
                return RedirectToAction("ProductCatalog", "Dashboard");
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
                existing.Price = model.Price;
                existing.CategoryId = model.CategoryId;
                existing.Description = model.Description?.Trim();
                existing.Barcode = model.Barcode?.Trim();

                await _context.SaveChangesAsync();
                TempData["Success"] = "Product updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "Unable to save changes. Please try again.";
            }
            
            return RedirectToAction("ProductCatalog", "Dashboard");
        }

        // POST /Product/Archive/{id}
        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Product archived." });
            }
            return Json(new { success = false, message = "Product not found." });
        }

        // POST /Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string name,
            string sku,
            string? barcode,
            decimal price,
            string? description,
            IFormFile? imageFile,
            int categoryId)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku))
            {
                TempData["Error"] = "Product name and SKU are required.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            // Duplicate SKU guard
            if (await _context.Products.AnyAsync(p => p.SKU == sku.Trim()))
            {
                TempData["Error"] = $"This SKU \"{sku.Trim()}\" is already assigned to another gadget.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            // Duplicate Barcode guard
            if (!string.IsNullOrWhiteSpace(barcode) && await _context.Products.AnyAsync(p => p.Barcode == barcode.Trim()))
            {
                TempData["Error"] = $"This Barcode \"{barcode.Trim()}\" is already assigned to another gadget.";
                return RedirectToAction("ProductCatalog", "Dashboard");
            }

            string? uploadedImageUrl = null;
            if (imageFile != null)
            {
                uploadedImageUrl = await _imageService.UploadImageAsync(imageFile);
            }

            _context.Products.Add(new Product
            {
                Name        = name.Trim(),
                SKU         = sku.Trim(),
                Barcode     = barcode?.Trim(),
                Price       = price,
                Description = description?.Trim(),
                ImageUrl    = uploadedImageUrl,
                CategoryId  = categoryId
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Product \"{name.Trim()}\" added to the catalog.";
            return RedirectToAction("ProductCatalog", "Dashboard");
        }
    }
}
