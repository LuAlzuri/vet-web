using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Controllers;

[Authorize(Roles = "Veterinario")]
public class ServiciosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CatalogoService<Servicio> _catalogo;
    public ServiciosController(ApplicationDbContext context, CatalogoService<Servicio> catalogo) { _context = context; _catalogo = catalogo; }
    private void Preparar() { ViewBag.Titulo = "Servicios"; ViewBag.Controlador = "Servicios"; }

    public async Task<IActionResult> Listado()
    {
        Preparar();
        var datos = await _context.Servicios.AsNoTracking().OrderBy(x => x.Descripcion)
            .Select(x => new CatalogoDto { Id = x.Id, Descripcion = x.Descripcion }).ToListAsync();
        return View("~/Views/Shared/Catalogo.cshtml", datos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        Preparar();
        if (id == 0) return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", new CatalogoDto());
        var entidad = await _context.Servicios.FindAsync(id);
        if (entidad is null) return NotFound();
        return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", new CatalogoDto { Id = entidad.Id, Descripcion = entidad.Descripcion });
    }

    [HttpPost]
    public async Task<IActionResult> Guardar(CatalogoDto dto)
    {
        Preparar();
        dto.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
        if (await _context.Servicios.AnyAsync(x => x.Id != dto.Id && x.Descripcion.ToLower() == dto.Descripcion.Trim().ToLower()))
            ModelState.AddModelError(nameof(dto.Descripcion), "Ya existe esa descripción.");
        if (!ModelState.IsValid) return PartialView("~/Views/Shared/_FormularioCatalogo.cshtml", dto);
        if (await _catalogo.GuardarAsync(dto) is null) return NotFound();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!await _catalogo.EliminarAsync(id)) return NotFound();
        return Json(new { success = true });
    }
}
