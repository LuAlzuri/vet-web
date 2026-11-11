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
    public CuposDiariosController(ApplicationDbContext context) => _context = context;
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
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var cupo = dto.Id == 0 ? new CupoDiario() : await _context.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {dto.Id} FOR UPDATE").SingleOrDefaultAsync();
        if (cupo is null) return NotFound();
        if (dto.Id != 0 && cupo.FechaCupo != dto.FechaCupo!.Value.Date && await _context.Turnos.IgnoreQueryFilters().AnyAsync(x => x.CupoDiarioId == dto.Id))
            throw new InvalidOperationException("No se puede cambiar la fecha de un cupo con turnos registrados.");
        cupo.FechaCupo = dto.FechaCupo!.Value.Date; cupo.NumeroCuposDisponibles = dto.NumeroCuposDisponibles;
        if (dto.Id == 0) _context.CuposDiarios.Add(cupo);
        await _context.SaveChangesAsync(); await transaction.CommitAsync(); return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var cupo = await _context.CuposDiarios.FindAsync(id); if (cupo is null) return NotFound();
        _context.CuposDiarios.Remove(cupo); await _context.SaveChangesAsync(); return Json(new { success = true });
    }
}
