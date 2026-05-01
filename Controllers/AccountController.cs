using Microsoft.AspNetCore.Mvc;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GadgetVault.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SignUp()

        
        {
            return View();
        }

        [HttpPost]
        public IActionResult SignUp(string username, string email, string password)
        {
            // Simple mock sign-up flow
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) 
            {
                ViewBag.Error = "Please enter both your email and password.";
                return View();
            }

            // [Professional Re-Seed]: Ensures the database has the standard roster with FullNames
            if (!_context.Users.Any(u => u.Username == "alex.admin"))
            {
                // Ensure Roles exist first
                var roles = _context.Roles.ToList();
                if (!roles.Any(r => r.Name == "Admin")) 
                {
                    _context.Roles.AddRange(
                        new Role { Name = "Admin", Description = "Super Admin" },
                        new Role { Name = "WarehouseManager", Description = "Warehouse Manager" },
                        new Role { Name = "WarehouseStaff", Description = "Warehouse Staff" },
                        new Role { Name = "SalesAndProcurement", Description = "Sales and Procurement" },
                        new Role { Name = "Supplier", Description = "External Supplier" }
                    );
                    _context.SaveChanges();
                    roles = _context.Roles.ToList();
                }
                else if (!roles.Any(r => r.Name == "Supplier"))
                {
                    _context.Roles.Add(new Role { Name = "Supplier", Description = "External Supplier" });
                    _context.SaveChanges();
                    roles = _context.Roles.ToList();
                }

                var adminRole = roles.First(r => r.Name == "Admin").Id;
                var mgrRole = roles.First(r => r.Name == "WarehouseManager").Id;
                var staffRole = roles.First(r => r.Name == "WarehouseStaff").Id;
                var salesRole = roles.First(r => r.Name == "SalesAndProcurement").Id;
                var suppRole = roles.First(r => r.Name == "Supplier").Id;

                // Wipe legacy/empty accounts to prevent crashes
                _context.Users.RemoveRange(_context.Users);
                _context.SaveChanges();

                var globalTech = _context.BusinessPartners.FirstOrDefault(p => p.CompanyName == "Global Tech Inc") 
                                 ?? new BusinessPartner { CompanyName = "Global Tech Inc", PartnerType = PartnerType.Supplier, IsActive = true };
                
                if (globalTech.Id == 0) { _context.BusinessPartners.Add(globalTech); _context.SaveChanges(); }

                // Synchronize existing POs with Global Tech for demo consistency
                var orphanedPOs = _context.PurchaseOrders.ToList();
                foreach (var po in orphanedPOs) { po.SupplierId = globalTech.Id; }
                _context.SaveChanges();

                // Ensure Mock PO for Supplier Demo
                if (!_context.PurchaseOrders.Any(o => o.SupplierId == globalTech.Id))
                {
                    _context.PurchaseOrders.Add(new PurchaseOrder {
                        PONumber = "PO-DEMO-001",
                        SupplierId = globalTech.Id,
                        OrderDate = System.DateTime.Now.AddDays(-1),
                        Status = POStatus.Ordered,
                        TotalAmount = 750.00m,
                        Items = new List<PurchaseOrderItem> {
                            new PurchaseOrderItem { ProductId = 1, Quantity = 10, UnitPrice = 75.00m }
                        }
                    });
                }

                _context.Users.AddRange(
                    new User { Username = "alex.admin", FullName = "Alex Ray", Email = "admin@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = adminRole, IsActive = true },
                    new User { Username = "jordan.mgr", FullName = "Jordan Chen", Email = "manager@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = mgrRole, IsActive = true },
                    new User { Username = "casey.staff", FullName = "Casey Miller", Email = "staff@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = staffRole, IsActive = true },
                    new User { Username = "morgan.sales", FullName = "Morgan Reed", Email = "sales@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = salesRole, IsActive = true },
                    new User { Username = "global.supplier", FullName = "Global Tech Supplier", Email = "global.supplier@gmail.com", PasswordHash = "P@ssword123", RoleId = suppRole, SupplierId = globalTech.Id, IsActive = true }
                );
                _context.SaveChanges();
            }

            // Actual Database Verification
            var user = _context.Users.Include(u => u.Role)
                .FirstOrDefault(u => (u.Email.ToLower() == email.ToLower() || u.Username.ToLower() == email.ToLower()) && u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials. Please use P@ssword123 for seeded accounts.";
                return View();
            }

            // Create claims for role-based access
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "Guest"),
                new Claim("FullName", user.FullName ?? user.Username),
                new Claim("RoleId", user.RoleId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("Cookies", principal);

            // Route logically based on Role Name
            return user.Role?.Name switch
            {
                "Admin"                => RedirectToAction("Index", "Dashboard"),
                "WarehouseManager"     => RedirectToAction("WarehouseManager", "Dashboard"),
                "WarehouseStaff"       => RedirectToAction("ReceiveStock", "Dashboard"), 
                "SalesAndProcurement"  => RedirectToAction("ProductCatalog", "Dashboard"),
                "Supplier"             => RedirectToAction("SupplierDashboard", "Dashboard"),
                _                      => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public IActionResult ExternalLogin(string provider)
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
