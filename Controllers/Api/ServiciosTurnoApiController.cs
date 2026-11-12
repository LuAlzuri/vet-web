using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers.Api;
[ApiController, Route("api/turnos/{turnoId:int}/servicios"), Authorize(Roles = "Veterinario,Tutor")]
public class ServiciosTurnoApiController(ApplicationDbContext db, TurnoService turnos, ServicioTurnoService servicios, UserManager<ApplicationUser> usuarios) : ControllerBase
{
    private string UsuarioId => usuarios.GetUserId(User)!;
    [HttpGet]
    public async Task<ActionResult<List<ServicioTurnoRespuestaDto>>> Listar(int turnoId)
    {
        if (!await turnos.ConsultarVisibles(UsuarioId, User.IsInRole("Veterinario")).AnyAsync(x => x.Id == turnoId)) return NotFound();
        return await db.ServiciosPorTurno.IgnoreQueryFilters().AsNoTracking().Where(x => x.TurnoId == turnoId).OrderBy(x => x.ServicioId)
            .Select(x => new ServicioTurnoRespuestaDto(x.ServicioId, x.Servicio.Descripcion, x.Completado)).ToListAsync();
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Crear(int turnoId, ServicioTurnoSolicitudDto dto)
    {
        await servicios.GuardarAsync(new ServicioPorTurnoDto { TurnoId = turnoId, ServicioId = dto.ServicioId, Completado = dto.Completado }, UsuarioId);
        return Created($"/api/turnos/{turnoId}/servicios", new { dto.ServicioId, dto.Completado });
    }
    [HttpPut("{servicioId:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Modificar(int turnoId, int servicioId, ServicioTurnoSolicitudDto dto)
    {
        if (dto.ServicioId != 0 && dto.ServicioId != servicioId) return BadRequest();
        return await servicios.GuardarAsync(new ServicioPorTurnoDto { EsEdicion = true, TurnoId = turnoId, ServicioId = servicioId, Completado = dto.Completado }, UsuarioId) ? NoContent() : NotFound();
    }
    [HttpDelete("{servicioId:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int turnoId, int servicioId) => await servicios.EliminarAsync(turnoId, servicioId, UsuarioId) ? NoContent() : NotFound();
}
