using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VetWeb.Controllers;

[Authorize(Roles = "Veterinario,Administrador")]
public class CatalogosController : Controller
{
    [HttpGet]
    public IActionResult Listado() => View();
}
