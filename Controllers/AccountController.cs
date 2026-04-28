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

                _context.Users.AddRange(
                    new User { Username = "alex.admin", FullName = "Alex Ray", Email = "admin@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = 1, IsActive = true },
                    new User { Username = "jordan.mgr", FullName = "Jordan Chen", Email = "manager@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = 2, IsActive = true },
                    new User { Username = "casey.staff", FullName = "Casey Miller", Email = "staff@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = 3, IsActive = true },
                    new User { Username = "morgan.sales", FullName = "Morgan Reed", Email = "sales@gadgetvault.com", PasswordHash = "P@ssword123", RoleId = 4, IsActive = true },
                    new User { Username = "global.vendor", FullName = "Global Tech Vendor", Email = "vendor@globaltech.com", PasswordHash = "P@ssword123", RoleId = 6, SupplierId = globalTech.Id, IsActive = true }
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

            // Route logically based on physical Role ID binding
            return user.RoleId switch
            {
                1 => RedirectToAction("Index", "Dashboard"),
                2 => RedirectToAction("WarehouseManager", "Dashboard"),
                3 => RedirectToAction("ReceiveStock", "Dashboard"), 
                4 => RedirectToAction("PurchaseOrders", "Dashboard"),
                6 => RedirectToAction("SupplierPortal", "Dashboard"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public IActionResult ExternalLogin(string provider)
        {
            return RedirectToAction("Index", "Home");
        }
    }
}
