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
        private readonly IConfiguration _configuration;

        public AccountController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // Public registration is disabled. This is a managed system.
        // All accounts are created by the Administrator via User Management.
        [HttpGet]
        public IActionResult SignUp() => RedirectToAction("Login");

        [HttpPost]
        public IActionResult SignUp(string username, string email, string password) => RedirectToAction("Login");

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var recaptchaResponse = Request.Form["g-recaptcha-response"];
            var secretKey = _configuration["RECAPTCHA_SECRET_KEY"];

            if (string.IsNullOrEmpty(recaptchaResponse))
            {
                ViewBag.Error = "Please complete the reCAPTCHA challenge.";
                return View();
            }

            using (var client = new System.Net.Http.HttpClient())
            {
                var content = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", secretKey ?? ""),
                    new KeyValuePair<string, string>("response", recaptchaResponse.ToString())
                });

                var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonDocument.Parse(jsonResponse);
                
                if (!result.RootElement.GetProperty("success").GetBoolean())
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "SECURITY_ALERT",
                        PerformedBy = "Anonymous",
                        Timestamp = DateTime.UtcNow,
                        Details = $"reCAPTCHA validation failed for attempt: {email}"
                    });
                    await _context.SaveChangesAsync();

                    ViewBag.Error = "Security verification failed. Please try again.";
                    return View();
                }
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) 
            {
                ViewBag.Error = "Please enter both your email and password.";
                return View();
            }

            // Secure verification using BCrypt
            var user = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() || u.Username.ToLower() == email.ToLower());

            if (user != null)
            {
                // 1. Check if account is disabled
                if (!user.IsActive)
                {
                    ViewBag.Error = "Account disabled. Please contact the System Manager.";
                    return View();
                }

                // 2. Check if currently locked out
                if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
                {
                    var waitMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                    ViewBag.Error = $"Account temporarily locked for {waitMinutes} more minutes.";
                    return View();
                }
            }

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                if (user != null)
                {
                    user.AccessFailedCount++;
                    
                    if (user.AccessFailedCount >= 10)
                    {
                        user.IsActive = false;
                        ViewBag.Error = "Account disabled due to multiple failed attempts. Please contact the System Manager.";
                        
                        _context.AuditLogs.Add(new AuditLog
                        {
                            Action = "SECURITY_ALERT",
                            PerformedBy = user.Username,
                            Timestamp = DateTime.UtcNow,
                            Details = $"Account PERMANENTLY DISABLED after 10 failed attempts for {email} from IP {ip}."
                        });
                    }
                    else if (user.AccessFailedCount == 5)
                    {
                        user.LockoutEnd = DateTime.UtcNow.AddMinutes(5);
                        ViewBag.Error = "Account temporarily locked for 5 minutes.";
                        
                        _context.AuditLogs.Add(new AuditLog
                        {
                            Action = "SECURITY_ALERT",
                            PerformedBy = user.Username,
                            Timestamp = DateTime.UtcNow,
                            Details = $"Account TEMPORARILY LOCKED (5 min) after 5 failed attempts for {email} from IP {ip}."
                        });
                    }
                    else
                    {
                        ViewBag.Error = "Invalid credentials. Access Denied.";
                    }
                }
                else
                {
                    ViewBag.Error = "Invalid credentials. Access Denied.";
                }

                // Record FAILED_LOGIN event
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "FAILED_LOGIN",
                    PerformedBy = user?.Username ?? "Anonymous",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Failed login attempt for {email} from IP {ip}."
                });
                await _context.SaveChangesAsync();

                return View();
            }

            // Successful Login - Reset failed count
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            // Create claims for role-based access
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Name ?? "Guest"),
                new Claim("FullName", user.FullName ?? user.Username),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("ProfilePictureUrl", user.ProfilePictureUrl ?? "")
            };

            // Add granular permissions as individual claims
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

            // Update Session Info
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Record LOGIN event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "LOGIN",
                PerformedBy = user.Username,
                Timestamp = DateTime.UtcNow,
                Details = $"User {user.Username} successfully logged in from IP {user.LastLoginIp}."
            });
            await _context.SaveChangesAsync();

            // Route logically based on Role Name
            return user.Role?.Name switch
            {
                "Admin"                => RedirectToAction("Index", "Dashboard"),
                "SystemManager"        => RedirectToAction("Index", "Dashboard"),
                "System Manager"        => RedirectToAction("Index", "Dashboard"),
                "WarehouseManager"     => RedirectToAction("WarehouseManager", "Dashboard"),
                "WarehouseStaff"       => RedirectToAction("LiveInventory", "Warehouse"), 
                "SalesAndProcurement"  => RedirectToAction("ProductCatalog", "Dashboard"),
                "Supplier"             => RedirectToAction("SupplierDashboard", "Dashboard"),
                _                      => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name ?? "Unknown";

            // Record LOGOUT event
            _context.AuditLogs.Add(new AuditLog
            {
                Action = "LOGOUT",
                PerformedBy = username,
                Timestamp = DateTime.UtcNow,
                Details = $"User {username} logged out of the system."
            });
            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync("Cookies");
            return RedirectToAction("Login");
        }
    }
}
