using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using GadgetVault.Models;
using GadgetVault.Data;
using GadgetVault.Services;
using System.Linq;

namespace GadgetVault.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ImageService _imageService;

        public SettingsController(ApplicationDbContext context, ImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var settings = _context.SystemSettings.FirstOrDefault();
            if (settings == null)
            {
                settings = new SystemSettings
                {
                    CompanyName = "GadgetVault",
                    PrimaryColorHex = "#4F46E5"
                };
                _context.SystemSettings.Add(settings);
                _context.SaveChanges();
            }

            return View("~/Views/Dashboard/CompanySettings.cshtml", settings);
        }

        [HttpPost]
        public async Task<IActionResult> Index(SystemSettings model, IFormFile? LogoFile)
        {
            if (ModelState.IsValid)
            {
                string? logoUrl = null;
                if (LogoFile != null)
                {
                    logoUrl = await _imageService.UploadImageAsync(LogoFile);
                }

                var existing = _context.SystemSettings.FirstOrDefault();
                if (existing != null)
                {
                    existing.CompanyName = model.CompanyName;
                    existing.PrimaryColorHex = model.PrimaryColorHex;
                    if (logoUrl != null)
                    {
                        existing.LogoUrl = logoUrl;
                    }
                    _context.SaveChanges();
                }
                else
                {
                    if (logoUrl != null)
                    {
                        model.LogoUrl = logoUrl;
                    }
                    _context.SystemSettings.Add(model);
                    _context.SaveChanges();
                }

                TempData["SuccessMessage"] = "Settings updated successfully.";
                return RedirectToAction("Index");
            }

            return View("~/Views/Dashboard/CompanySettings.cshtml", model);
        }
    }
}
