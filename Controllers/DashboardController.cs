using Microsoft.AspNetCore.Mvc;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GadgetVault.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Demonstrating how roles will route natively to their exact dashboard modules
        [HttpGet]
        public IActionResult Index(string roleUrlParam)
        {
            // For Demo purposes: Simulates evaluating the logged-in user's role and routing them cleanly
            if (roleUrlParam == "system") return RedirectToAction("SystemManager");
            if (roleUrlParam == "manager") return RedirectToAction("WarehouseManager");
            if (roleUrlParam == "staff") return RedirectToAction("WarehouseStaff");
            if (roleUrlParam == "sales") return RedirectToAction("SalesAndProcurement");

            // Default fallback 
            return RedirectToAction("SystemManager");
        }

        [HttpGet]
        public IActionResult SystemManager() { return View(); }

        [HttpGet]
        public IActionResult UserManagement(string searchString, int pageNumber = 1)
        {
            var query = _context.Users.Include(u => u.Role).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(u => u.Email.Contains(searchString));
            }

            int pageSize = 5;
            int totalItems = query.Count();
            int totalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var users = query.OrderByDescending(u => u.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Roles = _context.Roles.ToList();
            ViewBag.TotalUsers = _context.Users.Count();

            return View(users);
        }



        [HttpPost]
        public IActionResult InviteUser(string email, int roleId)
        {
            if (string.IsNullOrEmpty(email) || roleId == 0) return RedirectToAction("UserManagement");

            var newUser = new User
            {
                Email = email,
                Username = email.Split('@')[0], // Use part of email as temp username
                FullName = "New User",
                RoleId = roleId,
                CreatedAt = System.DateTime.UtcNow,
                IsActive = true,
                TwoFactorEnabled = false,
                PasswordHash = "default123"
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            return RedirectToAction("UserManagement");
        }



        [HttpPost]
        public IActionResult EditUser(int id, int newRoleId)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.RoleId = newRoleId;
                _context.SaveChanges();
            }
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public IActionResult ToggleUserStatus(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                _context.SaveChanges();
            }
            return RedirectToAction("UserManagement");
        }

        [HttpGet]
        public IActionResult WarehouseManager() { return View(); }

        [HttpGet]
        public IActionResult WarehouseStaff() { return View(); }

        [HttpGet]
        public IActionResult PickAndPack() { return View(); }

        [HttpGet]
        public IActionResult ReceiveStock() { return View(); }

        [HttpGet]
        public IActionResult StockAdjustments() { return View(); }

        [HttpGet]
        public IActionResult SalesAndProcurement() { return View(); }

        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username);
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> MasterData()
        {
            // Ensure seed data exists
            if (!await _context.Categories.AnyAsync())
            {
                _context.Categories.AddRange(
                    new Category { Name = "Enterprise Networking",  Description = "Switches, routers, access points, and enterprise LAN/WAN hardware." },
                    new Category { Name = "HPC Hardware",           Description = "High-performance compute cards, workstation CPUs, and server-grade memory." },
                    new Category { Name = "Workspace Solutions",    Description = "Monitors, docking stations, ergonomic peripherals, and collaboration tools." }
                );
                await _context.SaveChangesAsync();
            }

            if (!await _context.WarehouseLocations.AnyAsync())
            {
                _context.WarehouseLocations.AddRange(
                    new WarehouseLocation { Zone = "Z-A", Aisle = "High-Value Electronics", Rack = "4 Aisles", Bin = "" },
                    new WarehouseLocation { Zone = "Z-B", Aisle = "Bulk Storage", Rack = "12 Aisles", Bin = "" },
                    new WarehouseLocation { Zone = "Z-C", Aisle = "Receiving Staging", Rack = "N/A (Open Area)", Bin = "" },
                    new WarehouseLocation { Zone = "Z-D", Aisle = "Fragile & Returns", Rack = "2 Aisles", Bin = "" }
                );
                await _context.SaveChangesAsync();
            }

            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var locations = await _context.WarehouseLocations
                .OrderBy(l => l.Zone)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Locations = locations;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ProductCatalog()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Products   = products;
            ViewBag.Categories = categories;
            return View();
        }

        [HttpGet]
        public IActionResult RolesPermissions() { return View(); }

        [HttpGet]
        public IActionResult SecurityLogs() { return View(); }

        [HttpGet]
        public IActionResult SystemLogs() { return View(); }

        [HttpGet]
        public IActionResult CompanySettings() { return View(); }

        [HttpGet]
        public async Task<IActionResult> SupplierPortal()
        {
            // Identify the logged-in user. In this lab, we check User.Identity.
            // If not authenticated (for testing), we default to 'global_tech_vendor'.
            var currentUsername = User.Identity?.Name ?? "global.vendor";

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Supplier)
                .FirstOrDefaultAsync(u => u.Username == currentUsername);

            if (user == null) return View(new List<PurchaseOrder>());

            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .AsQueryable();

            // Strict Data Isolation
            if (user.Role?.Name != "SystemManager")
            {
                query = query.Where(o => o.SupplierId == user.SupplierId);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.CurrentUser = user;
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgePO(int id)
        {
            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po != null && po.Status == POStatus.Ordered)
            {
                po.Status = POStatus.Acknowledged;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"PO {po.PONumber} Acknowledged.";
            }
            return RedirectToAction("SupplierPortal");
        }

        [HttpGet]
        public async Task<IActionResult> SeedVendorUser()
        {
            var vendorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Vendor");
            if (vendorRole == null)
            {
                vendorRole = new Role { Name = "Vendor", Description = "External supplier access to POs" };
                _context.Roles.Add(vendorRole);
                await _context.SaveChangesAsync();
            }

            var partner = await _context.BusinessPartners.FirstOrDefaultAsync(p => p.CompanyName == "Global Tech Inc");
            if (partner == null)
            {
                partner = new BusinessPartner { CompanyName = "Global Tech Inc", ContactPerson = "John Tech", Email = "john@globaltech.com", PartnerType = PartnerType.Supplier, IsActive = true };
                _context.BusinessPartners.Add(partner);
                await _context.SaveChangesAsync();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == "global_tech_vendor");
            if (user == null)
            {
                user = new User
                {
                    Username = "global_tech_vendor",
                    Email = "vendor@globaltech.com",
                    FullName = "Global Tech Vendor",
                    PasswordHash = "password123",
                    RoleId = vendorRole.Id,
                    SupplierId = partner.Id,
                    IsActive = true
                };
                _context.Users.Add(user);
            }
            else
            {
                user.RoleId = vendorRole.Id;
                user.SupplierId = partner.Id;
            }

            await _context.SaveChangesAsync();
            return Content($"User 'global_tech_vendor' linked to {partner.CompanyName} (ID: {partner.Id}) successfully.");
        }

        [HttpGet]
        public async Task<IActionResult> PurchaseOrders(string? search, string? status)
        {
            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(o => o.PONumber.ToLower().Contains(s) || 
                                        (o.Supplier != null && o.Supplier.CompanyName.ToLower().Contains(s)));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<POStatus>(status, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            
            ViewBag.Suppliers = await _context.BusinessPartners
                .Where(p => p.IsActive && p.PartnerType == PartnerType.Supplier)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.TotalCount = await _context.PurchaseOrders.CountAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseOrder(int supplierId, DateTime orderDate, string? notes, List<PurchaseOrderItem> Items)
        {
            // Auto-generate PO Number: PO-YYYYMMDD-COUNT+1
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await _context.PurchaseOrders
                .CountAsync(o => o.PONumber.Contains(dateStr));
            var poNumber = $"PO-{dateStr}-{(countToday + 1).ToString("D3")}";

            // Calculate Total from items
            decimal totalAmount = 0;
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    totalAmount += item.Quantity * item.UnitPrice;
                }
            }

            var po = new PurchaseOrder
            {
                PONumber = poNumber,
                SupplierId = supplierId,
                OrderDate = orderDate,
                Status = POStatus.Draft,
                Notes = notes,
                TotalAmount = totalAmount,
                Items = Items ?? new List<PurchaseOrderItem>()
            };

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Purchase Order {poNumber} created successfully.";
            return RedirectToAction("PurchaseOrders");
        }

        [HttpGet]
        public IActionResult SalesOrders() { return View(); }

        [HttpGet]
        public async Task<IActionResult> BusinessDirectory()
        {
            var partners = await _context.BusinessPartners
                .Where(p => p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();
            return View(partners);
        }

        [HttpPost]
        public async Task<IActionResult> AddOrEditBusinessPartner(BusinessPartner model)
        {
            if (model.Id == 0)
            {
                _context.BusinessPartners.Add(model);
                TempData["Success"] = "Partner added successfully.";
            }
            else
            {
                var existing = await _context.BusinessPartners.FindAsync(model.Id);
                if (existing != null)
                {
                    existing.CompanyName = model.CompanyName;
                    existing.ContactPerson = model.ContactPerson;
                    existing.Email = model.Email;
                    existing.Phone = model.Phone;
                    existing.Address = model.Address;
                    existing.PartnerType = model.PartnerType;
                    TempData["Success"] = "Partner updated successfully.";
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("BusinessDirectory");
        }

        // ── Archive (soft-delete) ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchivePartner(int id)
        {
            var partner = await _context.BusinessPartners.FindAsync(id);
            if (partner != null)
            {
                partner.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{partner.CompanyName} has been archived.";
            }
            return RedirectToAction("BusinessDirectory");
        }

        // ── Recover from Archive ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecoverPartner(int id)
        {
            var partner = await _context.BusinessPartners.FindAsync(id);
            if (partner != null)
            {
                partner.IsActive = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"{partner.CompanyName} has been restored to the active directory." });
            }
            return Json(new { success = false, message = "Partner not found." });
        }

        // ── Get Archived Partners (JSON for vault panel) ───────────────────────
        [HttpGet]
        public async Task<IActionResult> GetArchivedPartners()
        {
            var archived = await _context.BusinessPartners
                .Where(p => !p.IsActive)
                .OrderBy(p => p.CompanyName)
                .Select(p => new { p.Id, p.CompanyName, p.ContactPerson, p.Email, p.Phone, p.PartnerType })
                .ToListAsync();
            return Json(archived);
        }
    }
}
