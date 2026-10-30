using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
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
public class PerrosPatologiasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CatalogosFormularioService _catalogos;
    private readonly IDataProtector _protector;
    public PerrosPatologiasController(ApplicationDbContext context, CatalogosFormularioService catalogos, IDataProtectionProvider protection)
    { _context = context; _catalogos = catalogos; _protector = protection.CreateProtector("VetWeb.Patologias.Perro"); }

    private string Contexto(int perroId) => $"{User.FindFirstValue(ClaimTypes.NameIdentifier)}:{perroId}";
    private bool ContextoValido(PerroPatologiaDto dto)
    {
        if (string.IsNullOrEmpty(dto.ContextoPerro)) return false;
        try { return _protector.Unprotect(dto.ContextoPerro) == Contexto(dto.PerroId); }
        catch (CryptographicException) { return false; }
    }
    public async Task<IActionResult> Listado(int perroId = 0)
    {
        var query = _context.PerrosPatologias.AsNoTracking().Include(x => x.Perro).Include(x => x.Patologia).AsQueryable();
        if (perroId != 0)
        {
            var perro = await _context.Perros.SingleOrDefaultAsync(x => x.Id == perroId);
            if (perro is null) return NotFound();
            ViewBag.PerroId = perroId;
            ViewBag.NombrePerro = perro.Nombre;
            query = query.Where(x => x.PerroId == perroId);
        }
        return View(await query.ToListAsync());
    }
    private async Task Preparar(int perroId)
    {
        ViewBag.NombrePerro = await _context.Perros.Where(x => x.Id == perroId).Select(x => x.Nombre).SingleOrDefaultAsync();
        ViewBag.Patologias = new SelectList(await _context.Patologias.OrderBy(x => x.Descripcion).ToListAsync(), "Id", "Descripcion").Append(new SelectListItem("Otro…", "-1"));
    }
    public async Task<IActionResult> ObtenerFormulario(int perroId = 0, int patologiaId = 0)
    {
        if (!await _context.Perros.AnyAsync(x => x.Id == perroId)) return NotFound();
        await Preparar(perroId);
        var contexto = _protector.Protect(Contexto(perroId));
        if (patologiaId == 0) return PartialView("_Formulario", new PerroPatologiaDto { PerroId = perroId, ContextoPerro = contexto });
        var x = await _context.PerrosPatologias.SingleOrDefaultAsync(x => x.PerroId == perroId && x.PatologiaId == patologiaId);
        if (x is null) return NotFound();
        return PartialView("_Formulario", new PerroPatologiaDto { EsEdicion = true, PerroId = x.PerroId, ContextoPerro = contexto, PatologiaId = x.PatologiaId, Controlado = x.Controlado, Observacion = x.Observacion });
    }
    [HttpPost]
    public async Task<IActionResult> Guardar(PerroPatologiaDto dto)
    {
        if (!ContextoValido(dto)) return Forbid();
        if (!await _context.Perros.AnyAsync(x => x.Id == dto.PerroId)) ModelState.AddModelError(nameof(dto.PerroId), "Seleccioná un perro activo.");
        if (dto.PatologiaId != CatalogosFormularioService.Otro && !await _context.Patologias.AnyAsync(x => x.Id == dto.PatologiaId))
            ModelState.AddModelError(nameof(dto.PatologiaId), "Seleccioná una patología válida.");
        if (dto.EsEdicion && dto.PatologiaId == CatalogosFormularioService.Otro)
            ModelState.AddModelError(nameof(dto.PatologiaId), "Para cambiar de patología, agregá una asociación nueva.");
        if (!ModelState.IsValid) { await Preparar(dto.PerroId); return PartialView("_Formulario", dto); }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var patologiaId = await _catalogos.PatologiaAsync(dto.PatologiaId, dto.NuevaPatologia);
        var relacion = await _context.PerrosPatologias.SingleOrDefaultAsync(x => x.PerroId == dto.PerroId && x.PatologiaId == patologiaId);
        if (!dto.EsEdicion && relacion is not null)
        {
            ModelState.AddModelError(string.Empty, "El perro ya tiene esa patología.");
            await Preparar(dto.PerroId); return PartialView("_Formulario", dto);
        }
        if (dto.EsEdicion && relacion is null) return NotFound();
        if (relacion is null) { relacion = new PerroPatologia { PerroId = dto.PerroId, PatologiaId = patologiaId }; _context.PerrosPatologias.Add(relacion); }
        relacion.Controlado = dto.Controlado; relacion.Observacion = dto.Observacion?.Trim();
        await _context.SaveChangesAsync(); await transaction.CommitAsync(); return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> Eliminar(int perroId, int patologiaId)
    {
        var relacion = await _context.PerrosPatologias.SingleOrDefaultAsync(x => x.PerroId == perroId && x.PatologiaId == patologiaId);
        if (relacion is null) return NotFound();
        _context.PerrosPatologias.Remove(relacion); await _context.SaveChangesAsync(); return Json(new { success = true });
    }
}
