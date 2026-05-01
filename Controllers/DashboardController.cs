using Microsoft.AspNetCore.Mvc;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace GadgetVault.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Routing based on Role Name Claim
            var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            return roleName switch
            {
                "Admin"                => RedirectToAction("Admin"),
                "WarehouseManager"     => RedirectToAction("WarehouseManager"),
                "WarehouseStaff"       => RedirectToAction("ReceiveStock"),
                "SalesAndProcurement"  => RedirectToAction("ProductCatalog"),
                "Supplier"             => RedirectToAction("SupplierDashboard"),
                _                      => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public async Task<IActionResult> Admin() 
        { 
            await LoadDashboardMetrics();
            return View(); 
        }

        [HttpGet]
        public IActionResult SupplierDashboard()
        {
            // Dedicated landing page for suppliers
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SupplierProfile()
        {
            // Distinct endpoint for supplier company info
            var username = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).Include(u => u.Supplier).FirstOrDefaultAsync(u => u.Username == username);
            return View("MyProfile", user); // Reuse MyProfile view but with a distinct URL
        }



        [HttpGet]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public IActionResult ToggleUserStatus(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                _context.SaveChanges();
                TempData["Success"] = $"User \"{user.Username}\" has been {(user.IsActive ? "activated" : "deactivated")}.";
            }
            return RedirectToAction("UserManagement");
        }

        [HttpGet]
        public async Task<IActionResult> WarehouseManager() 
        { 
            await LoadDashboardMetrics();
            return View(); 
        }

        private async Task LoadDashboardMetrics()
        {
            // 1. Global Inventory Value
            var totalValue = await _context.StockLevels
                .Include(s => s.Product)
                .Where(s => s.Product != null && s.Product.IsActive)
                .SumAsync(s => (decimal)s.Quantity * (s.Product != null ? s.Product.SellingPrice : 0));

            // 2. Recent Activity (System-Wide Feed)
            var recentActivity = await _context.InventoryTransactions
                .Include(t => t.Product)
                .OrderByDescending(t => t.Timestamp)
                .Take(5)
                .ToListAsync();

            // 3. Low Stock Alerts (Total Quantity < 10 for ACTIVE products)
            var lowStockItems = await _context.StockLevels
                .Include(s => s.Product)
                .Where(s => s.Product != null && s.Product.IsActive)
                .GroupBy(s => new { s.ProductId, ProductName = s.Product != null ? s.Product.Name : "N/A" })
                .Select(g => new { 
                    Name = g.Key.ProductName, 
                    Qty = g.Sum(s => s.Quantity) 
                })
                .Where(x => x.Qty < 10)
                .ToListAsync();

            ViewBag.InventoryValue = totalValue;
            ViewBag.RecentActivity = recentActivity;
            ViewBag.LowStockCount = lowStockItems.Count;
            ViewBag.LowStockItems = lowStockItems;
            
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            
            // 4. Total Revenue (Exclude Drafts)
            ViewBag.TotalRevenue = await _context.SalesOrders
                .Where(o => o.Status != SOStatus.Draft && o.Status != SOStatus.Cancelled)
                .SumAsync(o => o.TotalAmount);

            // 5. Active Orders (Picking or Packed)
            ViewBag.ActiveOrdersCount = await _context.SalesOrders
                .CountAsync(o => o.Status == SOStatus.Picking || o.Status == SOStatus.Packed);

            // 6. Recent Sales Orders (Last 5)
            ViewBag.RecentSalesOrders = await _context.SalesOrders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            ViewBag.PendingOrders = await _context.PurchaseOrders.CountAsync(o => o.Status == POStatus.Ordered);
        }

        [HttpGet]
        public async Task<IActionResult> WarehouseStaff() 
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

        [HttpGet]
        public IActionResult PickAndPack() { return View(); }

        [HttpGet]
        public async Task<IActionResult> ReceiveStock()
        {
            var acknowledgedOrders = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Where(o => o.Status == POStatus.Acknowledged)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var locations = await _context.WarehouseLocations
                .OrderBy(l => l.Zone)
                .ToListAsync();

            var recentTransactions = await _context.InventoryTransactions
                .Include(t => t.Product)
                .Include(t => t.Location)
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
            var suppliers = await _context.BusinessPartners
                .Where(p => p.PartnerType == PartnerType.Supplier && p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            ViewBag.AcknowledgedOrders = acknowledgedOrders;
            ViewBag.Locations = locations;
            ViewBag.RecentTransactions = recentTransactions;
            ViewBag.Products = products;
            ViewBag.Suppliers = suppliers;
            return View();
        }

        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> StockAdjustments() 
        { 
            var transactions = await _context.InventoryTransactions
                .Include(t => t.Product)
                .OrderByDescending(t => t.Timestamp)
                .Take(50)
                .ToListAsync();

            // Calculate KPIs
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            
            // 1. Pending (Mocked for now as we don't have an IsApproved flag, but set to 0 to show "Clear")
            ViewBag.PendingCount = 0; 
            
            // 2. Total Shrinkage (Negative adjustments this month)
            var shrinkage = await _context.InventoryTransactions
                .Where(t => t.Type == TransactionType.Adjustment && t.Quantity < 0 && t.Timestamp >= startOfMonth)
                .SumAsync(t => (decimal?)Math.Abs(t.Quantity) * (t.Product != null ? t.Product.CostPrice : 0)) ?? 0;
            ViewBag.TotalShrinkage = shrinkage;

            // 3. Audit Completion (Locations with activity vs total locations)
            var totalLocs = await _context.WarehouseLocations.CountAsync(l => l.IsActive);
            var activeLocs = await _context.InventoryTransactions
                .Where(t => t.Timestamp >= startOfMonth && t.LocationId != null)
                .Select(t => t.LocationId)
                .Distinct()
                .CountAsync();
            ViewBag.AuditCompletion = totalLocs > 0 ? (int)((double)activeLocs / totalLocs * 100) : 100;

            // Dropdowns for Manual Adjustment
            ViewBag.Products = await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            ViewBag.Locations = await _context.WarehouseLocations.Where(l => l.IsActive).OrderBy(l => l.Zone).ToListAsync();

            return View(transactions); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitStockAdjustment(int productId, int locationId, int quantityChange, string reason, string? notes)
        {
            if (productId == 0 || locationId == 0 || quantityChange == 0)
            {
                TempData["Error"] = "Invalid adjustment details.";
                return RedirectToAction("StockAdjustments");
            }

            // 1. Update/Create Stock Level
            var stock = await _context.StockLevels.FirstOrDefaultAsync(s => s.ProductId == productId && s.LocationId == locationId);
            if (stock == null)
            {
                stock = new StockLevel { ProductId = productId, LocationId = locationId, Quantity = 0 };
                _context.StockLevels.Add(stock);
            }
            stock.Quantity += quantityChange;

            // 2. Log Transaction
            var transaction = new InventoryTransaction
            {
                ProductId = productId,
                LocationId = locationId,
                Quantity = quantityChange,
                Type = TransactionType.Adjustment,
                Timestamp = DateTime.UtcNow,
                ReferenceId = $"MANUAL-{reason.ToUpper()}",
                Notes = notes
            };
            _context.InventoryTransactions.Add(transaction);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Stock adjustment for product recorded successfully.";
            return RedirectToAction("StockAdjustments");
        }

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
        [Authorize(Roles = "Admin, WarehouseManager")]
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
                .Where(c => c.IsActive)
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var locations = await _context.WarehouseLocations
                .Where(l => l.IsActive)
                .OrderBy(l => l.Zone)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Locations = locations;
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement, WarehouseStaff")]
        public async Task<IActionResult> ProductCatalog(string? search, string? category, int pageNumber = 1)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(s) || p.SKU.ToLower().Contains(s));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category != null && p.Category.Name == category);
            }

            int pageSize = 8; // Displaying 8 items per page as requested (range 5-10)
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var products = await query
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var suppliers = await _context.BusinessPartners
                .Where(p => p.PartnerType == PartnerType.Supplier && p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            // Fetch summed stock levels for internal staff
            var stockMap = await _context.StockLevels
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.TotalQty);

            ViewBag.Products   = products;
            ViewBag.Categories = categories;
            ViewBag.Suppliers  = suppliers;
            ViewBag.StockMap   = stockMap;
            ViewBag.ShowStock  = true;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalProducts = totalItems;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCategory = category;

            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult RolesPermissions() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult SecurityLogs() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult SystemLogs() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CompanySettings() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Supplier, Vendor, Admin")]
        public async Task<IActionResult> SupplierPortal()
        {
            // Identify the logged-in user. 
            var currentUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUsername)) return RedirectToAction("Login", "Account");

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Supplier)
                .FirstOrDefaultAsync(u => u.Username == currentUsername);

            if (user == null) return View(new List<PurchaseOrder>());

            // Fetch POs with LINQ
            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .AsQueryable();

            // Strict Data Isolation: Suppliers only see their own company's POs
            if (user.Role?.Name == "Supplier" || user.Role?.Name == "Vendor")
            {
                var supplierId = user.SupplierId ?? 0;
                query = query.Where(o => o.SupplierId == supplierId);
            }
            else if (user.Role?.Name != "Admin" && user.Role?.Name != "WarehouseManager" && user.Role?.Name != "SalesAndProcurement")
            {
                // Optional: restrict other roles if needed, but keeping it flexible for internal staff
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.CurrentUser = user;
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Supplier, Vendor, Admin")]
        public async Task<IActionResult> AcknowledgePO(int id)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == currentUsername);

            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po == null) return NotFound();

            // Security Check: Ensure supplier only acknowledges their own POs
            if (user != null && (user.Role?.Name == "Supplier" || user.Role?.Name == "Vendor"))
            {
                if (po.SupplierId != (user.SupplierId ?? 0))
                {
                    return Forbid();
                }
            }

            if (po.Status == POStatus.Ordered || po.Status == POStatus.Draft)
            {
                po.Status = POStatus.Acknowledged;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"PO {po.PONumber} Acknowledged.";
            }
            return RedirectToAction("SupplierPortal");
        }

        [Authorize(Roles = "Supplier, Vendor, Admin")]
        public async Task<IActionResult> OrderHistory(string search, int page = 1)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).Include(u => u.Supplier).FirstOrDefaultAsync(u => u.Username == currentUsername);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .Where(o => o.Status == POStatus.Received); 

            if (user.Role?.Name == "Supplier" || user.Role?.Name == "Vendor")
            {
                int supplierId = user.SupplierId ?? 0;
                query = query.Where(o => o.SupplierId == supplierId);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.PONumber.Contains(search));
            }

            int pageSize = 10;
            var totalOrders = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);
            ViewBag.CurrentUser = user;

            return View(orders);
        }

        [HttpGet]
        [Authorize(Roles = "Supplier, Vendor, Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> PODetails(int id)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == currentUsername);

            var po = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (po == null) return NotFound();

            // Security Check: Data Isolation for manual URL entry
            if (user != null && (user.Role?.Name == "Supplier" || user.Role?.Name == "Vendor"))
            {
                if (po.SupplierId != (user.SupplierId ?? 0))
                {
                    return Forbid(); 
                }
            }

            return View(po);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> InitializeDatabase()
        {
            // 1. Ensure Categories
            if (!await _context.Categories.AnyAsync())
            {
                _context.Categories.AddRange(
                    new Category { Name = "Enterprise Networking", Description = "Switches, routers, access points." },
                    new Category { Name = "HPC Hardware",           Description = "High-performance compute hardware." },
                    new Category { Name = "Workspace Solutions",    Description = "Monitors, chairs, and desks." }
                );
                await _context.SaveChangesAsync();
            }

            var netCat = await _context.Categories.FirstAsync(c => c.Name == "Enterprise Networking");
            var workCat = await _context.Categories.FirstAsync(c => c.Name == "Workspace Solutions");
            var hpcCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "HPC Hardware") ?? netCat;

            // 2. Ensure Role "Supplier" exists
            var supplierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Supplier");
            if (supplierRole == null)
            {
                supplierRole = new Role { Name = "Supplier", Description = "External supplier access to POs" };
                _context.Roles.Add(supplierRole);
                await _context.SaveChangesAsync();
            }

            // 3. Define Suppliers to Seed
            var suppliersToSeed = new List<BusinessPartner>
            {
                new BusinessPartner { CompanyName = "Titan Network", ContactPerson = "Atlas Titan", Email = "titannetwork@gmail.com", PartnerType = PartnerType.Supplier, IsActive = true },
                new BusinessPartner { CompanyName = "Elite Workspace", ContactPerson = "Sarah Elite", Email = "eliteworkspace@gmail.com", PartnerType = PartnerType.Supplier, IsActive = true }
            };

            foreach (var s in suppliersToSeed)
            {
                var existing = await _context.BusinessPartners.FirstOrDefaultAsync(p => p.CompanyName == s.CompanyName);
                if (existing == null)
                {
                    _context.BusinessPartners.Add(s);
                    await _context.SaveChangesAsync();
                }
            }

            // Refresh IDs
            var titan = await _context.BusinessPartners.FirstAsync(p => p.CompanyName == "Titan Network");
            var elite = await _context.BusinessPartners.FirstAsync(p => p.CompanyName == "Elite Workspace");

            // 4. Ensure Products
            if (!await _context.Products.AnyAsync(p => p.SupplierId == titan.Id))
            {
                _context.Products.AddRange(
                    new Product { Name = "Titan Core Switch 24P", SKU = "TN-SW24", CostPrice = 960.00m, SellingPrice = 1200.00m, CategoryId = netCat.Id, SupplierId = titan.Id, IsActive = true },
                    new Product { Name = "Titan Edge Router G2", SKU = "TN-RTG2", CostPrice = 680.00m, SellingPrice = 850.00m, CategoryId = netCat.Id, SupplierId = titan.Id, IsActive = true }
                );
            }

            if (!await _context.Products.AnyAsync(p => p.SupplierId == elite.Id))
            {
                _context.Products.AddRange(
                    new Product { Name = "Elite Ergo Chair Pro", SKU = "EW-CHPRO", CostPrice = 360.00m, SellingPrice = 450.00m, CategoryId = workCat.Id, SupplierId = elite.Id, IsActive = true },
                    new Product { Name = "Elite 4K UltraWide Monitor", SKU = "EW-MON4K", CostPrice = 720.00m, SellingPrice = 899.99m, CategoryId = workCat.Id, SupplierId = elite.Id, IsActive = true }
                );
            }

            await _context.SaveChangesAsync();

            // 5. PURGE: Delete all accounts containing "_vendor" to clean up terminology
            var oldVendorUsers = await _context.Users.Where(u => u.Username.Contains("_vendor") || u.Email.Contains("_vendor")).ToListAsync();
            if (oldVendorUsers.Any())
            {
                _context.Users.RemoveRange(oldVendorUsers);
                await _context.SaveChangesAsync();
            }

            // 6. Ensure User Accounts for ALL Suppliers
            var allSuppliers = await _context.BusinessPartners.Where(p => p.PartnerType == PartnerType.Supplier && p.IsActive).ToListAsync();
            var credentials = new System.Text.StringBuilder();
            credentials.AppendLine("<h1>Supplier Account Seeding Complete</h1>");
            credentials.AppendLine("<p style='color:green;'>Purged old '_vendor' accounts and regenerated with '_supplier' suffix.</p>");
            credentials.AppendLine("<table border='1' cellpadding='5' style='border-collapse:collapse;'>");
            credentials.AppendLine("<thead><tr><th>Supplier Name</th><th>Username</th><th>Email</th><th>Password</th><th>Status</th></tr></thead><tbody>");

            foreach (var sup in allSuppliers)
            {
                var username = sup.CompanyName.Replace(" ", "").Replace(".", "").Replace("&", "").ToLower() + "_supplier";
                var user = await _context.Users.FirstOrDefaultAsync(u => u.SupplierId == sup.Id);
                string status = "Linked";

                if (user == null)
                {
                    // Check if a user with this username exists but isn't linked
                    user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                    if (user != null)
                    {
                        user.SupplierId = sup.Id;
                        user.RoleId = supplierRole.Id;
                        user.FullName = sup.CompanyName + " Supplier";
                        status = "Re-linked";
                    }
                    else
                    {
                        user = new User
                        {
                            Username = username,
                            FullName = sup.CompanyName + " Supplier",
                            Email = username.Replace("_supplier", "") + "@gmail.com",
                            PasswordHash = "P@ssword123",
                            RoleId = supplierRole.Id,
                            SupplierId = sup.Id,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Users.Add(user);
                        status = "Created";
                    }
                }
                else
                {
                    user.RoleId = supplierRole.Id;
                    user.IsActive = true;
                    user.Username = username;
                    user.FullName = sup.CompanyName + " Supplier";
                    user.Email = username.Replace("_supplier", "") + "@gmail.com";
                    user.PasswordHash = "P@ssword123";
                }

                credentials.AppendLine($"<tr><td>{sup.CompanyName}</td><td>{username}</td><td>{user.Email}</td><td>P@ssword123</td><td>{status}</td></tr>");
            }

            await _context.SaveChangesAsync();
            credentials.AppendLine("</tbody></table>");
            credentials.AppendLine("<br/><a href='/Dashboard/ProductCatalog'>Go to Product Catalog</a>");

            return Content(credentials.ToString(), "text/html");
        }

        [HttpGet]
        public async Task<IActionResult> SeedSupplierUser()
        {
            // 1. Terminology Sync: Ensure "Supplier" role exists
            var supplierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Supplier");
            if (supplierRole == null)
            {
                supplierRole = new Role { Name = "Supplier", Description = "External supplier access to POs" };
                _context.Roles.Add(supplierRole);
                await _context.SaveChangesAsync();
            }

            var partner = await _context.BusinessPartners.FirstOrDefaultAsync(p => p.CompanyName == "Global Tech Inc");
            if (partner == null)
            {
                partner = new BusinessPartner { CompanyName = "Global Tech Inc", ContactPerson = "John Tech", Email = "john@globaltech.com", PartnerType = PartnerType.Supplier, IsActive = true };
                _context.BusinessPartners.Add(partner);
                await _context.SaveChangesAsync();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == "global_tech_supplier");
            if (user == null)
            {
                user = new User
                {
                    Username = "global_tech_supplier",
                    Email = "supplier@globaltech.com",
                    FullName = "Global Tech Supplier",
                    PasswordHash = "P@ssword123",
                    RoleId = supplierRole.Id,
                    SupplierId = partner.Id,
                    IsActive = true
                };
                _context.Users.Add(user);
            }
            else
            {
                user.RoleId = supplierRole.Id;
                user.SupplierId = partner.Id;
            }
            await _context.SaveChangesAsync();

            return Content($"User 'global_tech_supplier' linked to {partner.CompanyName} (ID: {partner.Id}) successfully.");
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> PurchaseOrders(string? search, string? status)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == currentUsername);

            // Security: Redirect suppliers to their portal if they try to access the full list
            if (user?.Role?.Name == "Supplier" || user?.Role?.Name == "Vendor")
            {
                return RedirectToAction("SupplierPortal");
            }

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
                .Where(p => p.PartnerType == PartnerType.Supplier && p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.TotalCount = await _context.PurchaseOrders.CountAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> CreatePurchaseOrder(int supplierId, DateTime orderDate, string? notes, List<PurchaseOrderItem> Items)
        {
            // Auto-generate PO Number: PO-YYYYMMDD-COUNT+1
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await _context.PurchaseOrders
                .CountAsync(o => o.PONumber.Contains(dateStr));
            var poNumber = $"PO-{dateStr}-{(countToday + 1).ToString("D3")}";

            // Calculate Total from items & Validate Supplier-Product match
            decimal totalAmount = 0;
            if (Items != null)
            {
                var productIds = Items.Select(i => i.ProductId).Distinct().ToList();
                var invalidProducts = await _context.Products
                    .Where(p => productIds.Contains(p.Id) && p.SupplierId != supplierId)
                    .Select(p => p.Name)
                    .ToListAsync();

                if (invalidProducts.Any())
                {
                    TempData["Error"] = $"Security Violation: The following products do not belong to the selected supplier: {string.Join(", ", invalidProducts)}";
                    return RedirectToAction("PurchaseOrders");
                }

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
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> SalesOrders()
        {
            ViewBag.Customers = await _context.BusinessPartners
                .Where(p => p.PartnerType == PartnerType.Customer && p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
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

        // â”€â”€ Archive (soft-delete) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // ── Recover from Archive ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecoverPartner(int id)
        {
            var partner = await _context.BusinessPartners.FindAsync(id);
            if (partner != null)
            {
                partner.IsActive = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Partner \"{partner.CompanyName}\" has been restored to the directory.";
                return Json(new { success = true, message = $"{partner.CompanyName} has been restored to the active directory." });
            }
            return Json(new { success = false, message = "Partner not found." });
        }
        // — Get Archived Partners (JSON for vault panel) —
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetArchivedPartners()
        {
            var archived = await _context.BusinessPartners
                .Where(p => !p.IsActive)
                .OrderBy(p => p.CompanyName)
                .Select(p => new { p.Id, p.CompanyName, p.ContactPerson, p.Email, p.Phone, p.PartnerType })
                .ToListAsync();
            return Json(archived);
        }

        // ——————————————————————————————————————————————
        [HttpGet]
        [Authorize(Roles = "Supplier, Admin")]
        public async Task<IActionResult> SupplierCatalog()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || user.SupplierId == null) return Unauthorized();

            // Dedicated landing page for supplier products
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.SupplierId == user.SupplierId)
                .OrderBy(p => p.Name)
                .ToListAsync();
            
            ViewBag.ShowStock = false;
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("ProductCatalog", products); 
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> GetArchivedProducts()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsActive);

            // Isolation: Suppliers only see their own archives
            if (User.IsInRole("Supplier"))
            {
                query = query.Where(p => p.SupplierId == user.SupplierId);
            }

            var archived = await query
                .OrderBy(p => p.Name)
                .Select(p => new {
                    id = p.Id,
                    sku = p.SKU ?? "N/A",
                    name = p.Name ?? "Unnamed Product",
                    price = User.IsInRole("Supplier") ? p.CostPrice : p.SellingPrice,
                    category = p.Category != null ? new { name = p.Category.Name } : null
                })
                .ToListAsync();

            return Json(archived);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, WarehouseManager, Supplier")]
        public async Task<IActionResult> RestoreProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsActive = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Gadget \"{product.Name}\" has been restored to the active catalog.";
                return Json(new { success = true, message = "Product restored." });
            }
            return Json(new { success = false, message = "Product not found." });
        }

        // â”€â”€ Permanent Delete â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> HardDeletePartner(int id)
        {
            var partner = await _context.BusinessPartners.FindAsync(id);
            if (partner != null)
            {
                // Note: In a real system, you'd check for related POs/SOs before hard deleting.
                // For this task, we proceed as requested for the duplicate.
                _context.BusinessPartners.Remove(partner);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{partner.CompanyName} has been permanently removed.";
            }
            return RedirectToAction("BusinessDirectory");
        }
    }
}
