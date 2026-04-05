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
        public IActionResult SalesAndProcurement() { return View(); }
    }
}
