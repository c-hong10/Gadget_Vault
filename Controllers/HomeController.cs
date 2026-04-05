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

}
