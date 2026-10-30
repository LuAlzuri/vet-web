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
public class TurnosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly TurnoService _turnos;
    public TurnosController(ApplicationDbContext context, UserManager<ApplicationUser> usuarios, TurnoService turnos)
    { _context = context; _usuarios = usuarios; _turnos = turnos; }
    private IQueryable<Turno> Consultar(bool personal = false)
    {
        var query = _context.Turnos.IgnoreQueryFilters().AsNoTracking().Include(x => x.Perro).ThenInclude(x => x.Tutor)
            .Include(x => x.CupoDiario).Include(x => x.Estado).Include(x => x.Veterinario).ThenInclude(x => x!.Usuario).Include(x => x.Tratamiento).AsQueryable();
        if (personal || !User.IsInRole("Veterinario")) query = query.Where(x => x.Perro.Tutor.UserId == _usuarios.GetUserId(User));
        else query = query.Where(x => x.VeterinarioId == null || x.Veterinario!.UserId == _usuarios.GetUserId(User));
        return query;
    }
    public async Task<IActionResult> Listado()
    {
        ViewBag.Personal = !User.IsInRole("Veterinario");
        return View(await Consultar().OrderByDescending(x => x.CupoDiario.FechaCupo).ToListAsync());
    }
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> MisTurnos()
    {
        ViewBag.Personal = true;
        return View("Listado", await Consultar(true).OrderByDescending(x => x.CupoDiario.FechaCupo).ToListAsync());
    }
    public async Task<IActionResult> Historial(int perroId, bool personal = false)
    {
        if (personal && !User.IsInRole("Tutor")) return Forbid();
        ViewBag.Personal = personal || !User.IsInRole("Veterinario");
        var perro = await _context.Perros.IgnoreQueryFilters().Include(x => x.Tutor).SingleOrDefaultAsync(x => x.Id == perroId);
        if (perro is null) return NotFound();
        if ((personal || !User.IsInRole("Veterinario")) && perro.Tutor.UserId != _usuarios.GetUserId(User)) return Forbid();
        ViewBag.Perro = perro.Nombre;
        return View("Listado", await Consultar(personal).Where(x => x.PerroId == perroId).OrderByDescending(x => x.CupoDiario.FechaCupo).ToListAsync());
    }
    private async Task Preparar(TurnoDto dto, bool personal = false)
    {
        var perros = _context.Perros.Where(x => x.Tutor.Usuario.Activo);
        if (personal || !User.IsInRole("Veterinario")) perros = perros.Where(x => x.Tutor.UserId == _usuarios.GetUserId(User));
        ViewBag.Perros = new SelectList(await perros.Select(x => new { x.Id, Nombre = x.Nombre + " (" + x.Tutor.Usuario.Apellido + ")" }).ToListAsync(), "Id", "Nombre");
        ViewBag.Veterinarios = new SelectList(await _context.Veterinarios.Where(x => x.Usuario.Activo).Select(x => new { x.Id, Nombre = x.Usuario.Nombre + " " + x.Usuario.Apellido }).ToListAsync(), "Id", "Nombre");
        var cupos = await _context.CuposDiarios.Where(x => x.Id == dto.CupoDiarioId || x.FechaCupo >= Reloj.Hoy && x.NumeroCuposDisponibles > 0).OrderBy(x => x.FechaCupo).ToListAsync();
        ViewBag.Cupos = new SelectList(cupos.Select(x => new { x.Id, Descripcion = x.FechaCupo.ToString("dd/MM/yyyy") + " — " + x.NumeroCuposDisponibles + " disponibles" }), "Id", "Descripcion");
        ViewBag.Tratamientos = new SelectList(await _context.Tratamientos.OrderBy(x => x.Descripcion).ToListAsync(), "Id", "Descripcion").Append(new SelectListItem("Otro…", "-1"));
        ViewBag.Servicios = await _context.Servicios.OrderBy(x => x.Descripcion).ToListAsync();
    }
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        ViewBag.Personal = !User.IsInRole("Veterinario");
        var dto = new TurnoDto();
        if (id != 0)
        {
            if (!User.IsInRole("Veterinario")) return Forbid();
            var x = await _context.Turnos.SingleOrDefaultAsync(x => x.Id == id);
            if (x is null) return NotFound();
            if (!await _turnos.PuedeAtenderAsync(x, _usuarios.GetUserId(User)!)) return Forbid();
            dto = new TurnoDto { Id = x.Id, PerroId = x.PerroId, VeterinarioId = x.VeterinarioId, CupoDiarioId = x.CupoDiarioId, TratamientoId = x.TratamientoId, Observaciones = x.Observaciones };
        }
        await Preparar(dto); return PartialView("_Formulario", dto);
    }
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> SolicitarPersonal()
    {
        ViewBag.Personal = true;
        var dto = new TurnoDto();
        await Preparar(dto, true);
        return PartialView("_Formulario", dto);
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> GuardarPersonal(TurnoDto dto)
    {
        ViewBag.Personal = true;
        if (dto.Id != 0 || dto.TratamientoId is not null || !string.IsNullOrWhiteSpace(dto.NuevoTratamiento)) return Forbid();
        if (!ModelState.IsValid) { await Preparar(dto, true); return PartialView("_Formulario", dto); }
        await _turnos.GuardarAsync(dto, _usuarios.GetUserId(User)!, false);
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Tutor")]
    public async Task<IActionResult> CancelarPersonal(int id)
    {
        await _turnos.CambiarEstadoAsync(id, EstadosTurno.CanceladoTutor, _usuarios.GetUserId(User)!, false);
        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> Guardar(TurnoDto dto)
    {
        ViewBag.Personal = !User.IsInRole("Veterinario");
        if (!User.IsInRole("Veterinario") && (dto.TratamientoId is not null || !string.IsNullOrWhiteSpace(dto.NuevoTratamiento))) return Forbid();
        if (!ModelState.IsValid) { await Preparar(dto); return PartialView("_Formulario", dto); }
        await _turnos.GuardarAsync(dto, _usuarios.GetUserId(User)!, User.IsInRole("Veterinario"));
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Confirmar(int id) { await _turnos.CambiarEstadoAsync(id, EstadosTurno.Confirmado, _usuarios.GetUserId(User)!, true); return Json(new { success = true }); }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Rechazar(int id) { await _turnos.CambiarEstadoAsync(id, EstadosTurno.Rechazado, _usuarios.GetUserId(User)!, true); return Json(new { success = true }); }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Completar(int id) { await _turnos.CambiarEstadoAsync(id, EstadosTurno.Completado, _usuarios.GetUserId(User)!, true); return Json(new { success = true }); }
    [HttpPost]
    public async Task<IActionResult> Cancelar(int id)
    {
        var veterinario = User.IsInRole("Veterinario");
        await _turnos.CambiarEstadoAsync(id, veterinario ? EstadosTurno.CanceladoVeterinario : EstadosTurno.CanceladoTutor, _usuarios.GetUserId(User)!, veterinario);
        return Json(new { success = true });
    }
}
