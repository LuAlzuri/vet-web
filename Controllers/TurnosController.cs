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
    private readonly VetoService _vetos;
    public TurnosController(ApplicationDbContext context, UserManager<ApplicationUser> usuarios, TurnoService turnos, VetoService vetos)
    { _context = context; _usuarios = usuarios; _turnos = turnos; _vetos = vetos; }
    private IQueryable<Turno> Consultar(bool personal = false)
    {
        return _turnos.ConsultarVisibles(_usuarios.GetUserId(User)!, !personal && User.IsInRole("Veterinario"))
            .Include(x => x.Perro).ThenInclude(x => x.Tutor)
            .Include(x => x.CupoDiario).Include(x => x.Estado).Include(x => x.Veterinario).ThenInclude(x => x!.Usuario).Include(x => x.Tratamientos).ThenInclude(x => x.Tratamiento).AsQueryable();
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
    private async Task Preparar(TurnoDto dto, bool personal = false, int? tutorPresencialId = null)
    {
        var perros = _context.Perros.Where(x => x.Tutor.Usuario.Activo);
        if (personal || !User.IsInRole("Veterinario")) perros = perros.Where(x => x.Tutor.UserId == _usuarios.GetUserId(User));
        if (tutorPresencialId.HasValue) perros = perros.Where(x => x.TutorId == tutorPresencialId.Value);
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
            dto = new TurnoDto { Id = x.Id, PerroId = x.PerroId, VeterinarioId = x.VeterinarioId, CupoDiarioId = x.CupoDiarioId, TratamientoId = await _context.TratamientosPorTurno.Where(t => t.TurnoId == x.Id).Select(t => (int?)t.TratamientoId).FirstOrDefaultAsync(), Observaciones = x.Observaciones };
        }
        await Preparar(dto); return PartialView("_Formulario", dto);
    }
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> SolicitarPersonal()
    {
        var usuarioId = _usuarios.GetUserId(User);
        if (await _vetos.Vigentes().AnyAsync(x => x.Tutor.UserId == usuarioId))
            throw new InvalidOperationException("Tenés un veto vigente. Comunicate con la veterinaria para solicitar una reserva presencial.");
        ViewBag.Personal = true;
        var dto = new TurnoDto();
        await Preparar(dto, true);
        return PartialView("_Formulario", dto);
    }
    [HttpGet, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> SolicitarPresencial(int tutorId)
    {
        if (!await _vetos.Vigentes().AnyAsync(x => x.TutorId == tutorId && x.Tutor.Usuario.Activo))
            throw new InvalidOperationException("El tutor indicado no tiene un veto vigente.");
        return await FormularioPresencial(tutorId, new TurnoDto());
    }

    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> GuardarPresencial(int tutorId, TurnoDto dto)
    {
        if (dto.Id != 0) return BadRequest();
        if (!await _context.Perros.AnyAsync(x => x.Id == dto.PerroId && x.TutorId == tutorId))
            ModelState.AddModelError(nameof(dto.PerroId), "Seleccioná un perro de este tutor.");
        if (!ModelState.IsValid) return await FormularioPresencial(tutorId, dto);
        await _turnos.ReservarPresencialAsync(dto, _usuarios.GetUserId(User)!);
        return Json(new { success = true });
    }

    private async Task<IActionResult> FormularioPresencial(int tutorId, TurnoDto dto)
    {
        ViewBag.TutorPresencialId = tutorId;
        await Preparar(dto, tutorPresencialId: tutorId);
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
    [HttpGet, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ObtenerConfirmacion(int id)
    {
        var turno = await _context.Turnos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (turno is null) return NotFound();
        if (!await _turnos.PuedeAtenderAsync(turno, _usuarios.GetUserId(User)!)) return Forbid();
        if (turno.EstadoId != EstadosTurno.Solicitado)
            throw new InvalidOperationException("Solo se pueden confirmar turnos solicitados.");
        return PartialView("_Confirmar", new ConfirmarTurnoDto { Id = id, Cantidad = turno.CantidadReservada });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Confirmar(ConfirmarTurnoDto dto)
    {
        if (!ModelState.IsValid) return PartialView("_Confirmar", dto);
        await _turnos.CambiarEstadoAsync(dto.Id, EstadosTurno.Confirmado, _usuarios.GetUserId(User)!, true, dto.Cantidad);
        return Json(new { success = true });
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Rechazar(int id) { await _turnos.CambiarEstadoAsync(id, EstadosTurno.Rechazado, _usuarios.GetUserId(User)!, true); return Json(new { success = true }); }
    [HttpGet, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ObtenerAtencion(int id)
    {
        var turno = await PrepararAtencion(id);
        var dto = new CompletarTurnoDto { Id = id, Observaciones = turno.Observaciones,
            ServicioIdsCompletados = await _context.ServiciosPorTurno.Where(x => x.TurnoId == id && x.Completado).Select(x => x.ServicioId).ToListAsync(),
            Tratamientos = await _context.TratamientosPorTurno.Where(x => x.TurnoId == id)
                .Select(x => new TratamientoAtencionDto { TratamientoId = x.TratamientoId, Razon = x.Razon }).ToListAsync() };
        return PartialView("_Atencion", dto);
    }

    private async Task<Turno> PrepararAtencion(int id)
    {
        var turno = await _context.Turnos.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("No se encontró el turno.");
        if (!await _turnos.PuedeAtenderAsync(turno, _usuarios.GetUserId(User)!)) throw new UnauthorizedAccessException();
        if (turno.EstadoId != EstadosTurno.Confirmado) throw new InvalidOperationException("Solo se registra la atención de turnos confirmados.");
        var fecha = await _context.CuposDiarios.Where(x => x.Id == turno.CupoDiarioId).Select(x => x.FechaCupo).SingleAsync();
        if (fecha > Reloj.Hoy) throw new InvalidOperationException("No se puede completar el turno antes de su fecha.");
        ViewBag.ServiciosAtencion = await _context.ServiciosPorTurno.Where(x => x.TurnoId == id).Select(x => x.Servicio).OrderBy(x => x.Descripcion).ToListAsync();
        ViewBag.TratamientosAtencion = await _context.Tratamientos.OrderBy(x => x.Descripcion).ToListAsync();
        return turno;
    }

    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Completar(CompletarTurnoDto dto)
    {
        if (!ModelState.IsValid) { await PrepararAtencion(dto.Id); return PartialView("_Atencion", dto); }
        await _turnos.CompletarAsync(dto.Id, dto, _usuarios.GetUserId(User)!);
        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> Cancelar(int id)
    {
        var veterinario = User.IsInRole("Veterinario");
        await _turnos.CambiarEstadoAsync(id, veterinario ? EstadosTurno.CanceladoVeterinario : EstadosTurno.CanceladoTutor, _usuarios.GetUserId(User)!, veterinario);
        return Json(new { success = true });
    }
}
