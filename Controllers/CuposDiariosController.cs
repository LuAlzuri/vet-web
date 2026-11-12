using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers;
[Authorize(Roles = "Veterinario,Tutor")]
public class CuposDiariosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CupoService _cupos;
    public CuposDiariosController(ApplicationDbContext context, CupoService cupos) { _context = context; _cupos = cupos; }
    public Task<IActionResult> Listado() => ListarAsync(false);
    [Authorize(Roles = "Tutor")]
    public Task<IActionResult> Disponibles() => ListarAsync(true);
    private async Task<IActionResult> ListarAsync(bool personal)
    {
        ViewBag.Personal = personal || !User.IsInRole("Veterinario");
        var query = _context.CuposDiarios.AsNoTracking();
        if (personal || !User.IsInRole("Veterinario")) query = query.Where(x => x.FechaCupo >= Reloj.Hoy && x.NumeroCuposDisponibles > 0);
        return View("Listado", await query.OrderBy(x => x.FechaCupo).ToListAsync());
    }
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        if (id == 0) return PartialView("_Formulario", new CupoDiarioDto { FechaCupo = Reloj.Hoy });
        var x = await _context.CuposDiarios.FindAsync(id); if (x is null) return NotFound();
        return PartialView("_Formulario", new CupoDiarioDto { Id = x.Id, FechaCupo = x.FechaCupo, NumeroCuposDisponibles = x.NumeroCuposDisponibles });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Guardar(CupoDiarioDto dto)
    {
        if (dto.FechaCupo < Reloj.Hoy) ModelState.AddModelError(nameof(dto.FechaCupo), "No se pueden publicar cupos en fechas pasadas.");
        if (!ModelState.IsValid) return PartialView("_Formulario", dto);
        if (await _cupos.GuardarAsync(dto) is null) return NotFound();
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!await _cupos.EliminarAsync(id)) return NotFound();
        return Json(new { success = true });
    }
}
