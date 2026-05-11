using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GadgetVault.Models;

namespace GadgetVault.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? id)
    {
        // We can handle different status codes here if needed (e.g. 404 vs 500)
        ViewBag.StatusCode = id;
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
