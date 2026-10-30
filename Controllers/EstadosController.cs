using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
namespace VetWeb.Controllers;
[Authorize(Roles = "Veterinario")]
public class EstadosController : Controller
{
    private readonly ApplicationDbContext _context;
    public EstadosController(ApplicationDbContext context) => _context = context;
    public async Task<IActionResult> Listado() => View(await _context.Estados.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
    [HttpGet]
    public IActionResult ObtenerFormulario() => Forbid();
    [HttpPost]
    public IActionResult Guardar() => Forbid();
    [HttpPost]
    public IActionResult Eliminar() => Forbid();
}
