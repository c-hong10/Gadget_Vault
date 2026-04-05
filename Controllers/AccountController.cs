using Microsoft.AspNetCore.Mvc;
using GadgetVault.Data;
using GadgetVault.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) 
            {
                ViewBag.Error = "Please enter both your email and password.";
                return View();
            }

            // [Auto-Seed Feature]: Ensures SSMS db is populated if completely empty
            if (!_context.Users.Any())
            {
                _context.Users.AddRange(
                    new User { Username = "System Admin", Email = "admin@gadgetvault.com", PasswordHash = "password123", RoleId = 1 },
                    new User { Username = "Warehouse Manager", Email = "manager@gadgetvault.com", PasswordHash = "password123", RoleId = 2 },
                    new User { Username = "Warehouse Staff", Email = "staff@gadgetvault.com", PasswordHash = "password123", RoleId = 3 },
                    new User { Username = "Sales Officer", Email = "sales@gadgetvault.com", PasswordHash = "password123", RoleId = 4 }
                    
                );
                _context.SaveChanges();
            }

            // Actual Database SSMS Verification
            var user = _context.Users.Include(u => u.Role)
                .FirstOrDefault(u => u.Email.ToLower() == email.ToLower() && u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password. Please verify your credentials.";
                return View();
            }

            // Route logically based on physical SSMS Role binding
            return user.Role?.Name switch
            {
                "SystemManager" => RedirectToAction("SystemManager", "Dashboard"),
                "WarehouseManager" => RedirectToAction("WarehouseManager", "Dashboard"),
                "WarehouseStaff" => RedirectToAction("WarehouseStaff", "Dashboard"),
                "SalesAndProcurement" => RedirectToAction("SalesAndProcurement", "Dashboard"),
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
