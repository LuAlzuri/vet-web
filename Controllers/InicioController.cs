using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Models;

namespace VetWeb.Controllers;

public class InicioController : Controller
{
    public IActionResult Indice() => View();

    public IActionResult Privacidad() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
