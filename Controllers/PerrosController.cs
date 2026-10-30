using System.Data;
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
public class PerrosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly ImagenPerroService _imagenes;
    public PerrosController(ApplicationDbContext context, UserManager<ApplicationUser> usuarios, ImagenPerroService imagenes) { _context = context; _usuarios = usuarios; _imagenes = imagenes; }
    public Task<IActionResult> Listado() => ListarAsync(false);

    [Authorize(Roles = "Tutor")]
    public Task<IActionResult> MisPerros() => ListarAsync(true);

    private async Task<IActionResult> ListarAsync(bool personal)
    {
        ViewBag.Personal = personal || !User.IsInRole("Veterinario");
        var query = _context.Perros.AsNoTracking().Include(x => x.Tutor).ThenInclude(x => x.Usuario)
            .Include(x => x.GrupoEtario).Include(x => x.Tamanio).AsQueryable();
        if (personal || !User.IsInRole("Veterinario")) query = query.Where(x => x.Tutor.UserId == _usuarios.GetUserId(User));
        return View("Listado", await query.OrderBy(x => x.Nombre).ToListAsync());
    }
    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        var perro = await _context.Perros.AsNoTracking().Include(x => x.Tutor).SingleOrDefaultAsync(x => x.Id == id);
        if (perro is null || perro.FotoArchivo is null) return NotFound();
        if (!User.IsInRole("Veterinario") && perro.Tutor.UserId != _usuarios.GetUserId(User)) return Forbid();
        var ruta = _imagenes.Ruta(perro.FotoArchivo);
        if (!System.IO.File.Exists(ruta)) return NotFound();
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(ruta, perro.FotoArchivo.EndsWith(".png") ? "image/png" : "image/jpeg");
    }

    private async Task Preparar()
    {
        ViewBag.Tutores = new SelectList(await _context.Tutores.Where(x => x.Usuario.Activo)
            .Select(x => new { x.Id, Nombre = x.Usuario.Apellido + ", " + x.Usuario.Nombre }).ToListAsync(), "Id", "Nombre");
        ViewBag.Grupos = new SelectList(await _context.GruposEtarios.OrderBy(x => x.Descripcion).ToListAsync(), "Id", "Descripcion");
        ViewBag.Tamanios = new SelectList(await _context.Tamanios.OrderBy(x => x.Descripcion).ToListAsync(), "Id", "Descripcion");
    }
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        await Preparar();
        if (id == 0) return PartialView("_Formulario", new PerroDto());
        var x = await _context.Perros.FindAsync(id);
        if (x is null) return NotFound();
        return PartialView("_Formulario", new PerroDto { Id = x.Id, TieneFoto = x.FotoArchivo != null, Nombre = x.Nombre, FechaNacimiento = x.FechaNacimiento,
            TutorId = x.TutorId, GrupoEtarioId = x.GrupoEtarioId, TamanioId = x.TamanioId, Reactivo = x.Reactivo, CalendarioVacunasCompleto = x.CalendarioVacunasCompleto });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
    public async Task<IActionResult> Guardar(PerroDto dto)
    {
        if (dto.FechaNacimiento > Reloj.Hoy || dto.FechaNacimiento == DateTime.MinValue)
            ModelState.AddModelError(nameof(dto.FechaNacimiento), "La fecha debe ser válida y no puede ser futura.");
        if (!await _context.Tutores.AnyAsync(x => x.Id == dto.TutorId && x.Usuario.Activo)) ModelState.AddModelError(nameof(dto.TutorId), "Seleccioná un tutor activo.");
        if (!await _context.GruposEtarios.AnyAsync(x => x.Id == dto.GrupoEtarioId)) ModelState.AddModelError(nameof(dto.GrupoEtarioId), "Seleccioná un grupo etario válido.");
        if (!await _context.Tamanios.AnyAsync(x => x.Id == dto.TamanioId)) ModelState.AddModelError(nameof(dto.TamanioId), "Seleccioná un tamaño válido.");
        ImagenPerroService.FotoValidada? foto = null;
        if (ModelState.IsValid && dto.Foto is not null)
        {
            try { foto = await _imagenes.ValidarAsync(dto.Foto); }
            catch (InvalidOperationException error) { ModelState.AddModelError(nameof(dto.Foto), error.Message); }
        }
        if (!ModelState.IsValid)
        {
            dto.TieneFoto = dto.Id != 0 && await _context.Perros.AnyAsync(x => x.Id == dto.Id && x.FotoArchivo != null);
            await Preparar(); return PartialView("_Formulario", dto);
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var perro = dto.Id == 0 ? new Perro() : await _context.Perros.FindAsync(dto.Id);
        if (perro is null) return NotFound();
        if (dto.Id != 0 && perro.TutorId != dto.TutorId && await _context.Turnos.IgnoreQueryFilters().AnyAsync(x => x.PerroId == dto.Id))
            throw new InvalidOperationException("No se puede cambiar el tutor de un perro con historial de turnos.");
        perro.Nombre = dto.Nombre.Trim(); perro.FechaNacimiento = dto.FechaNacimiento!.Value.Date;
        perro.TutorId = dto.TutorId;
        perro.GrupoEtarioId = dto.GrupoEtarioId;
        perro.TamanioId = dto.TamanioId;
        perro.Reactivo = dto.Reactivo; perro.CalendarioVacunasCompleto = dto.CalendarioVacunasCompleto;
        var fotoAnterior = perro.FotoArchivo;
        string? fotoNueva = null;
        try
        {
            if (foto is not null) { fotoNueva = await _imagenes.GuardarAsync(foto); perro.FotoArchivo = fotoNueva; }
            if (dto.Id == 0) _context.Perros.Add(perro);
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
        }
        catch { _imagenes.Eliminar(fotoNueva); throw; }
        if (fotoNueva is not null) _imagenes.Eliminar(fotoAnterior);
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var perro = await _context.Perros.FindAsync(id); if (perro is null) return NotFound();
        if (await _context.Turnos.AnyAsync(x => x.PerroId == id && (x.EstadoId == EstadosTurno.Solicitado || x.EstadoId == EstadosTurno.Confirmado)))
            throw new InvalidOperationException("Cancelá o completá los turnos pendientes antes de dar de baja al perro.");
        perro.Eliminado = true; await _context.SaveChangesAsync(); await transaction.CommitAsync(); return Json(new { success = true });
    }
}
