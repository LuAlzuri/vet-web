using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Controllers;

[Authorize(Roles = "Veterinario")]
public class TratamientosController : Controller
{
    private readonly ApplicationDbContext _context;
    public TratamientosController(ApplicationDbContext context) => _context = context;
    private void Preparar() { ViewBag.Titulo = "Tratamientos"; ViewBag.Controlador = "Tratamientos"; }

    public async Task<IActionResult> Listado()
    {
        Preparar();
        var datos = await _context.Tratamientos.AsNoTracking().OrderBy(x => x.Descripcion)
            .Select(x => new CatalogoDto { Id = x.Id, Descripcion = x.Descripcion }).ToListAsync();
        return View("~/Views/Shared/Catalogo.cshtml", datos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        Preparar();
        if (id == 0) return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", new CatalogoDto());
        var entidad = await _context.Tratamientos.FindAsync(id);
        if (entidad is null) return NotFound();
        return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", new CatalogoDto { Id = entidad.Id, Descripcion = entidad.Descripcion });
    }

    [HttpPost]
    public async Task<IActionResult> Guardar(CatalogoDto dto)
    {
        Preparar();
        dto.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
        if (await _context.Tratamientos.AnyAsync(x => x.Id != dto.Id && x.Descripcion.ToLower() == dto.Descripcion.Trim().ToLower()))
            ModelState.AddModelError(nameof(dto.Descripcion), "Ya existe esa descripción.");
        if (!ModelState.IsValid) return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", dto);
        var entidad = dto.Id == 0 ? new Tratamiento() : await _context.Tratamientos.FindAsync(dto.Id);
        if (entidad is null) return NotFound();
        entidad.Descripcion = dto.Descripcion.Trim();
        if (dto.Id == 0) _context.Tratamientos.Add(entidad);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var entidad = await _context.Tratamientos.FindAsync(id);
        if (entidad is null) return NotFound();
        _context.Tratamientos.Remove(entidad);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
}
