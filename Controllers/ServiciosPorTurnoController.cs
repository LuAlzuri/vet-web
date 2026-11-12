using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers;
[Authorize(Roles = "Veterinario,Tutor")]
public class ServiciosPorTurnoController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly TurnoService _turnos;
    private readonly ServicioTurnoService _servicios;
    public ServiciosPorTurnoController(ApplicationDbContext context, UserManager<ApplicationUser> usuarios, TurnoService turnos, ServicioTurnoService servicios) { _servicios = servicios; _context = context; _usuarios = usuarios; _turnos = turnos; }
    public async Task<IActionResult> Listado(int turnoId, bool personal = false)
    {
        if (personal && !User.IsInRole("Tutor")) return Forbid();
        ViewBag.Personal = personal || !User.IsInRole("Veterinario");
        var turno = await _context.Turnos.IgnoreQueryFilters().Include(x => x.Perro).ThenInclude(x => x.Tutor).SingleOrDefaultAsync(x => x.Id == turnoId);
        if (turno is null) return NotFound();
        if ((personal || !User.IsInRole("Veterinario")) && turno.Perro.Tutor.UserId != _usuarios.GetUserId(User)) return Forbid();
        if (User.IsInRole("Veterinario") && !await _turnos.PuedeAtenderAsync(turno, _usuarios.GetUserId(User)!)) return Forbid();
        ViewBag.TurnoId = turnoId; ViewBag.Editable = EstadosTurno.Activo(turno.EstadoId) && User.IsInRole("Veterinario") && !personal;
        return View(await _context.ServiciosPorTurno.IgnoreQueryFilters().AsNoTracking().Include(x => x.Servicio).Where(x => x.TurnoId == turnoId).ToListAsync());
    }
    private async Task Preparar() => ViewBag.Servicios = new SelectList(await _context.Servicios.OrderBy(x => x.Descripcion).ToListAsync(), "Id", "Descripcion");
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ObtenerFormulario(int turnoId, int servicioId = 0)
    {
        var turno = await _context.Turnos.SingleOrDefaultAsync(x => x.Id == turnoId);
        if (turno is null) return NotFound();
        if (!await _turnos.PuedeAtenderAsync(turno, _usuarios.GetUserId(User)!)) return Forbid();
        await Preparar(); if (servicioId == 0) return PartialView("_Formulario", new ServicioPorTurnoDto { TurnoId = turnoId });
        var x = await _context.ServiciosPorTurno.SingleOrDefaultAsync(x => x.TurnoId == turnoId && x.ServicioId == servicioId);
        if (x is null) return NotFound();
        return PartialView("_Formulario", new ServicioPorTurnoDto { EsEdicion = true, TurnoId = turnoId, ServicioId = servicioId, Completado = x.Completado });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Guardar(ServicioPorTurnoDto dto)
    {
        if (!await _context.Servicios.AnyAsync(x => x.Id == dto.ServicioId)) ModelState.AddModelError(nameof(dto.ServicioId), "Seleccioná un servicio válido.");
        if (!ModelState.IsValid) { await Preparar(); return PartialView("_Formulario", dto); }
        if (!await _servicios.GuardarAsync(dto, _usuarios.GetUserId(User)!)) return NotFound();
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int turnoId, int servicioId)
    {
        if (!await _servicios.EliminarAsync(turnoId, servicioId, _usuarios.GetUserId(User)!)) return NotFound();
        return Json(new { success = true });
    }
}
