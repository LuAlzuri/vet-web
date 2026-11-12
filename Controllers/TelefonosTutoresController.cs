using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers;
[Authorize(Roles = "Veterinario")]
public class TelefonosTutoresController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly TelefonoTutorService _telefonos;
    public TelefonosTutoresController(ApplicationDbContext context, TelefonoTutorService telefonos) { _context = context; _telefonos = telefonos; }
    public async Task<IActionResult> Listado() => View(await _context.TelefonosTutores.AsNoTracking().Include(x => x.Tutor).ThenInclude(x => x.Usuario).ToListAsync());
    private async Task Preparar() => ViewBag.Tutores = new SelectList(await _context.Tutores.Where(x => x.Usuario.Activo)
        .Select(x => new { x.Id, Nombre = x.Usuario.Apellido + ", " + x.Usuario.Nombre }).ToListAsync(), "Id", "Nombre");
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        await Preparar(); if (id == 0) return PartialView("_Formulario", new TelefonoTutorDto());
        var x = await _context.TelefonosTutores.FindAsync(id); if (x is null) return NotFound();
        return PartialView("_Formulario", new TelefonoTutorDto { Id = x.Id, TutorId = x.TutorId, NombrePropietario = x.NombrePropietario, Numero = x.Numero });
    }
    [HttpPost]
    public async Task<IActionResult> Guardar(TelefonoTutorDto dto)
    {
        if (!await _context.Tutores.AnyAsync(x => x.Id == dto.TutorId && x.Usuario.Activo)) ModelState.AddModelError(nameof(dto.TutorId), "Seleccioná un tutor activo.");
        if (!ModelState.IsValid) { await Preparar(); return PartialView("_Formulario", dto); }
        if (await _telefonos.GuardarAsync(dto) is null) return NotFound();
        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!await _telefonos.EliminarAsync(id)) return NotFound();
        return Json(new { success = true });
    }
}
