using Microsoft.AspNetCore.Mvc;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GadgetVault.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GadgetVault.Services.ImageService _imageService;

        public DashboardController(ApplicationDbContext context, GadgetVault.Services.ImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Routing based on Role Name Claim
            var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            return roleName switch
            {
                "Admin"                => RedirectToAction("Admin"),
                "SystemManager"        => RedirectToAction("Admin"),
                "System Manager"        => RedirectToAction("Admin"),
                "WarehouseManager"     => RedirectToAction("WarehouseManager"),
                "WarehouseStaff"       => RedirectToAction("LiveInventory", "Warehouse"),
                "SalesAndProcurement"  => RedirectToAction("ProductCatalog"),
                "Supplier"             => RedirectToAction("SupplierDashboard"),
                _                      => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SystemManager")]
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

        [HttpPost]
        public async Task<IActionResult> SubmitQuickSupport(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "Please enter a message before submitting.";
                return RedirectToAction("SupplierDashboard");
            }

            var username = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Supplier).FirstOrDefaultAsync(u => u.Username == username);
            var supplierName = user?.Supplier?.CompanyName ?? "Unknown Supplier";

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "SUPPLIER_MESSAGE",
                PerformedBy = username ?? "Anonymous",
                Timestamp = DateTime.UtcNow,
                Details = $"{message} | Sent by {supplierName}"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Your message has been logged. The Admin will be notified.";
            return RedirectToAction("SupplierDashboard");
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
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
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

            // Force 50 users for smooth JS pagination
            var users = query.OrderByDescending(u => u.CreatedAt).Take(50).ToList();

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Roles = _context.Roles.ToList();
            ViewBag.TotalUsers = _context.Users.Count();

            return View(users);
        }



        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
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
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("default123")
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            // Record USER_MANAGEMENT event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "USER_MANAGEMENT",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = System.DateTime.UtcNow,
                Details = $"Created user: {newUser.Username} (Role ID: {roleId})"
            });
            _context.SaveChanges();

            return RedirectToAction("UserManagement");
        }



        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
        public IActionResult ResetLockout(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
                user.IsActive = true;
                _context.SaveChanges();

                // Record SECURITY_ALERT event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "SECURITY_ALERT",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Manually reset security counters and activated user: {user.Username}"
                });
                _context.SaveChanges();

                TempData["Success"] = $"User \"{user.Username}\" has been fully restored.";
            }
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
        public IActionResult EditUser(int id, int newRoleId)
        {
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                var oldRole = user.Role?.Name ?? "None";
                user.RoleId = newRoleId;
                _context.SaveChanges();

                // Reload for name
                var newRole = _context.Roles.Find(newRoleId)?.Name ?? "Unknown";

                // Record ROLE_CHANGE event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "ROLE_CHANGE",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = System.DateTime.UtcNow,
                    Details = $"Changed role for {user.Username} from {oldRole} to {newRole}"
                });
                _context.SaveChanges();
            }
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
        public IActionResult ToggleUserStatus(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                
                // Reset security counters if activating
                if (user.IsActive)
                {
                    user.AccessFailedCount = 0;
                    user.LockoutEnd = null;
                }

                _context.SaveChanges();

                // Record USER_MANAGEMENT event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "USER_MANAGEMENT",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = System.DateTime.UtcNow,
                    Details = $"{(user.IsActive ? "Activated" : "Deactivated")} user: {user.Username}"
                });
                _context.SaveChanges();

                TempData["Success"] = $"User \"{user.Username}\" has been {(user.IsActive ? "activated" : "deactivated")}.";
            }
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SystemManager, System Manager")]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                var username = user.Username;
                _context.Users.Remove(user);
                _context.SaveChanges();

                // Record USER_MANAGEMENT event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "USER_MANAGEMENT",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = System.DateTime.UtcNow,
                    Details = $"Permanently deleted user: {username}"
                });
                _context.SaveChanges();

                TempData["Success"] = $"User \"{username}\" has been permanently removed.";
            }
            return RedirectToAction("UserManagement");
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager")]
        public async Task<IActionResult> WarehouseManager() 
        { 
            await LoadDashboardMetrics();

            // Fetch Active Operations (Picking or Packed)
            ViewBag.ActiveOperations = await _context.SalesOrders
                .Include(o => o.Customer)
                .Where(o => o.Status == SOStatus.Picking || o.Status == SOStatus.Packed)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

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

            // 3. Low Stock Alerts (Using global LowStockThreshold)
            var settings = await _context.SystemSettings.FirstOrDefaultAsync() ?? new SystemSettings();
            int threshold = settings.LowStockThreshold;

            var lowStockItems = await _context.StockLevels
                .Include(s => s.Product)
                .Include(s => s.Location)
                .Where(s => s.Product != null && s.Product.IsActive && s.Quantity <= threshold)
                .Select(s => new { 
                    Name = s.Product.Name, 
                    Qty = s.Quantity,
                    Location = s.Location != null ? $"{s.Location.Zone}-{s.Location.Aisle}" : "Unknown"
                })
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
            
            // 7. Active Floor Staff (WarehouseStaff Role)
            ViewBag.ActiveStaffCount = await _context.Users
                .Include(u => u.Role)
                .CountAsync(u => u.Role != null && u.Role.Name == "WarehouseStaff" && u.IsActive);
        }


        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, WarehouseStaff")]
        public IActionResult PickAndPack() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, WarehouseStaff")]
        public async Task<IActionResult> ReceiveStock()
        {
            var acknowledgedOrders = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Where(o => o.Status == POStatus.Acknowledged || o.Status == POStatus.Shipped)
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
        [Authorize(Roles = "Admin, WarehouseManager")]
        public async Task<IActionResult> StockAdjustments() 
        { 
            var transactions = await _context.InventoryTransactions
                .Include(t => t.Product)
                .OrderByDescending(t => t.Timestamp)
                .Take(50)
                .ToListAsync();

            // Calculate KPIs
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            
            // 1. Pending (Removed as per instant execution policy)
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
        [Authorize(Roles = "Admin, WarehouseManager")]
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
                Notes = notes,
                IsApproved = true // Instant execution
            };
            _context.InventoryTransactions.Add(transaction);

            // 3. Audit Log (Phase 5)
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "Stock Adjustment",
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                EntityName = "Product",
                EntityId = productId,
                Details = $"Adjusted quantity by {quantityChange}. Reason: {reason}. Notes: {notes ?? "N/A"}"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Stock adjustment for product recorded successfully.";
            return RedirectToAction("StockAdjustments");
        }

        [HttpGet]
        public IActionResult SalesAndProcurement() { return View(); }

        [HttpGet]
        public async Task<IActionResult> MyProfile(string tab = "profile")
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username);
            ViewBag.ActiveTab = tab;
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string fullName, string username, string phoneNumber, string address, IFormFile? profilePicture)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == currentUsername);
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Phone Validation (11-digit numeric)
            var cleanPhone = new string((phoneNumber ?? "").Where(char.IsDigit).ToArray());
            if (cleanPhone.Length != 11)
            {
                TempData["Error"] = "Phone Number must be exactly 11 numeric digits.";
                return RedirectToAction("MyProfile");
            }

            // 2. Handle Cloudinary Upload
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Delete old image if it exists
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    await _imageService.DeleteImageAsync(user.ProfilePictureUrl);
                }

                // Upload new image
                var newUrl = await _imageService.UploadProfilePictureAsync(profilePicture);
                if (!string.IsNullOrEmpty(newUrl))
                {
                    user.ProfilePictureUrl = newUrl;
                }
            }

            // 3. Update Text Fields
            user.FullName = fullName;
            user.Username = username;
            user.PhoneNumber = cleanPhone;
            user.Address = address;

            await _context.SaveChangesAsync();

            // 4. Refresh authentication session (Stale UI Fix)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "Guest"),
                new Claim("FullName", user.FullName ?? user.Username),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("ProfilePictureUrl", user.ProfilePictureUrl ?? "") // Sync image to UI claims
            };

            if (!string.IsNullOrEmpty(user.Role?.Permissions))
            {
                var perms = user.Role.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in perms)
                {
                    claims.Add(new Claim("Permission", p));
                }
            }

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("Cookies", principal);

            // 5. Record Audit Event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "ACCOUNT_SECURITY",
                PerformedBy = currentUsername ?? "System",
                Timestamp = System.DateTime.UtcNow,
                Details = $"User updated profile. Fields: Name, Username, Phone, Address, Avatar."
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile information and photo updated successfully.";
            return RedirectToAction("MyProfile");
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var currentUsername = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Verify Current Password using BCrypt
            if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                TempData["Error"] = "The current password you entered is incorrect.";
                return RedirectToAction("MyProfile", new { tab = "security" });
            }

            // 2. Verify Match
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "The new password and confirmation do not match.";
                return RedirectToAction("MyProfile", new { tab = "security" });
            }

            // 3. Strict Validation: 12+ chars, Upper, Lower, Number, Symbol
            bool hasUpper = newPassword.Any(char.IsUpper);
            bool hasLower = newPassword.Any(char.IsLower);
            bool hasNumber = newPassword.Any(char.IsDigit);
            bool hasSymbol = newPassword.Any(c => !char.IsLetterOrDigit(c));

            if (newPassword.Length < 12 || !hasUpper || !hasLower || !hasNumber || !hasSymbol)
            {
                TempData["Error"] = "Password must be at least 12 characters and include Uppercase, Lowercase, Number, and Symbol.";
                return RedirectToAction("MyProfile", new { tab = "security" });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            // Record ACCOUNT_SECURITY event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "ACCOUNT_SECURITY",
                PerformedBy = currentUsername ?? "System",
                Timestamp = System.DateTime.UtcNow,
                Details = "User updated their password to a hardened format."
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password hardened and updated successfully.";
            return RedirectToAction("MyProfile", new { tab = "security" });
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
        public async Task<IActionResult> RolesPermissions()
        {
            var roles = await _context.Roles
                .Include(r => r.Users)
                .Where(r => r.Name != "Customer")
                .ToListAsync();
            return View(roles);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRole(string name, string description, string[] selectedPermissions)
        {
            var role = new Role 
            { 
                Name = name, 
                Description = description,
                Permissions = selectedPermissions != null ? string.Join(",", selectedPermissions) : ""
            };
            _context.Roles.Add(role);
            
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "ROLE_CHANGE",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Created new system role: {name}"
            });
            
            await _context.SaveChangesAsync();
            return RedirectToAction("RolesPermissions");
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(int id, string name, string description, string[] selectedPermissions)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound();

            role.Name = name;
            role.Description = description;
            role.Permissions = selectedPermissions != null ? string.Join(",", selectedPermissions) : "";

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "ROLE_CHANGE",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Modified role: {name}"
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("RolesPermissions");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SecurityLogs()
        {
            string[] securityKeywords = { "LOGIN", "LOGOUT", "FAILED_LOGIN", "USER_MANAGEMENT", "ROLE_CHANGE", "ACCOUNT_SECURITY", "SYSTEM INITIALIZATION", "SECURITY_ALERT" };
            
            var logs = await _context.AuditLogs
                .Where(l => securityKeywords.Contains(l.Action.ToUpper()))
                .OrderByDescending(l => l.Timestamp)
                .Take(50)
                .ToListAsync();
            return View(logs);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportSecurityLogsCsv()
        {
            string[] securityKeywords = { "LOGIN", "LOGOUT", "FAILED_LOGIN", "USER_MANAGEMENT", "ROLE_CHANGE", "ACCOUNT_SECURITY", "SYSTEM INITIALIZATION", "SECURITY_ALERT" };

            var logs = await _context.AuditLogs
                .Where(l => securityKeywords.Contains(l.Action.ToUpper()))
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Event Type,User,Description");

            foreach (var log in logs)
            {
                // Escape commas in description by wrapping in quotes
                var description = log.Details?.Replace("\"", "\"\"") ?? "";
                csv.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.Action},{log.PerformedBy},\"{description}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "SecurityLogs.csv");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SystemLogs()
        {
            string[] securityKeywords = { "LOGIN", "LOGOUT", "FAILED_LOGIN", "USER_MANAGEMENT", "ROLE_CHANGE", "ACCOUNT_SECURITY", "SYSTEM INITIALIZATION", "SECURITY_ALERT" };

            var logs = await _context.AuditLogs
                .Where(l => !securityKeywords.Contains(l.Action.ToUpper()))
                .OrderByDescending(l => l.Timestamp)
                .Take(50)
                .ToListAsync();
            
            return View(logs);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CompanySettings() { return View(); }

        [HttpGet]
        [Authorize(Roles = "Supplier, Vendor, Admin")]
        public async Task<IActionResult> SupplierPortal(int page = 1)
        {
            var currentUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUsername)) return RedirectToAction("Login", "Account");

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Supplier)
                .FirstOrDefaultAsync(u => u.Username == currentUsername);

            if (user == null) return View(new List<PurchaseOrder>());

            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                // Filter: Active orders ONLY (Not yet Received)
                .Where(o => o.Status != POStatus.Received);

            if (user.Role?.Name == "Supplier" || user.Role?.Name == "Vendor")
            {
                var supplierId = user.SupplierId ?? 0;
                query = query.Where(o => o.SupplierId == supplierId);
            }

            int pageSize = 10;
            var totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentUser = user;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Supplier, Vendor, Admin")]
        public async Task<IActionResult> ShipPO(int id)
        {
            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po == null) return NotFound();

            if (po.Status == POStatus.Acknowledged)
            {
                po.Status = POStatus.Shipped;
                await _context.SaveChangesAsync();
                
                _context.AuditLogs.Add(new AuditLog {
                    Action = "PO_SHIPPED",
                    PerformedBy = User.Identity?.Name ?? "Supplier",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Supplier marked PO {po.PONumber} as SHIPPED."
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Order {po.PONumber} marked as Shipped.";
            }
            return RedirectToAction("SupplierPortal");
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

                // Record PO_ACKNOWLEDGED event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "PO_ACKNOWLEDGED",
                    PerformedBy = User.Identity?.Name ?? "System",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Acknowledged PO: {po.PONumber} (Supplier: {user?.Supplier?.CompanyName ?? "External Partner"})"
                });
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

            var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
            
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

            // Record PO_CREATED event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "PO_CREATED",
                PerformedBy = User.Identity?.Name ?? "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Created PO: {poNumber} (Items: {po.Items.Count}, Total: {totalAmount:C})"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Purchase Order {poNumber} created and sent to supplier.";
            return RedirectToAction("PurchaseOrders");
        }

        [HttpGet]
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> SalesOrders()
        {
            var query = _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .OrderByDescending(o => o.Id);

            var orders = await query.ToListAsync();

            ViewBag.TotalOrders = orders.Count;
            ViewBag.DraftCount = orders.Count(o => o.Status == SOStatus.Draft);
            ViewBag.PickingCount = orders.Count(o => o.Status == SOStatus.Picking);
            ViewBag.ShippedCount = orders.Count(o => o.Status == SOStatus.Shipped);

            ViewBag.Customers = await _context.BusinessPartners
                .Where(p => p.PartnerType == PartnerType.Customer && p.IsActive)
                .OrderBy(p => p.CompanyName)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(orders);
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
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
        public async Task<IActionResult> AddOrEditBusinessPartner(BusinessPartner model)
        {
            // 1. Normalize Phone (numeric only)
            if (!string.IsNullOrEmpty(model.Phone))
            {
                model.Phone = new string(model.Phone.Where(char.IsDigit).ToArray());
            }

            // 2. Uniqueness Checks (Email and Phone)
            var duplicate = await _context.BusinessPartners
                .AnyAsync(p => p.Id != model.Id && p.IsActive && 
                          ((!string.IsNullOrEmpty(model.Email) && p.Email == model.Email) || 
                           (!string.IsNullOrEmpty(model.Phone) && p.Phone == model.Phone)));

            if (duplicate)
            {
                TempData["Error"] = "This Email or Phone Number is already associated with an existing partner.";
                return RedirectToAction("BusinessDirectory");
            }

            // 3. Conditional Requirements Logic
            if (model.PartnerType == PartnerType.Supplier)
            {
                if (string.IsNullOrEmpty(model.CompanyName) || string.IsNullOrEmpty(model.ContactPerson) || 
                    string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Phone) || string.IsNullOrEmpty(model.Address))
                {
                    TempData["Error"] = "Suppliers require Company Name, Contact Person, Email, Phone, and Address.";
                    return RedirectToAction("BusinessDirectory");
                }
            }
            else // Customer
            {
                if (string.IsNullOrEmpty(model.CompanyName) || string.IsNullOrEmpty(model.ContactPerson) || string.IsNullOrEmpty(model.Phone))
                {
                    TempData["Error"] = "Customers require Company Name, Contact Person, and Phone Number.";
                    return RedirectToAction("BusinessDirectory");
                }
            }

            // 4. Phone Format Enforcement (Backend)
            if (model.Phone?.Length != 11)
            {
                TempData["Error"] = "Phone Number must be exactly 11 numeric digits.";
                return RedirectToAction("BusinessDirectory");
            }

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
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
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
        [Authorize(Roles = "Admin, WarehouseManager, SalesAndProcurement")]
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
        public async Task<IActionResult> SupplierCatalog(int page = 1)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || user.SupplierId == null) return Unauthorized();

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.SupplierId == user.SupplierId);

            int pageSize = 12;
            var totalCount = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            ViewBag.ShowStock = false;
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;

            return View(products); 
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
