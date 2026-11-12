using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/turnos")]
[Authorize(Roles = "Veterinario,Tutor")]
public class TurnosApiController(TurnoService turnos, UserManager<ApplicationUser> usuarios) : ControllerBase
{
    private string UsuarioId => usuarios.GetUserId(User)!;
    private bool EsVeterinario => User.IsInRole("Veterinario");

    private IQueryable<Turno> Consultar() => turnos.ConsultarVisibles(UsuarioId, EsVeterinario);

    private static IQueryable<TurnoRespuestaDto> Proyectar(IQueryable<Turno> consulta) => consulta
        .Select(x => new TurnoRespuestaDto(x.Id, x.PerroId, x.Perro.Nombre, x.CupoDiarioId, x.CupoDiario.FechaCupo,
            x.CantidadReservada, x.VeterinarioId, x.EstadoId, x.Estado.Descripcion, x.Observaciones));

    [HttpGet]
    public Task<List<TurnoRespuestaDto>> Listar([Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20)
        => Proyectar(Consultar().OrderByDescending(x => x.CupoDiario.FechaCupo).ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TurnoRespuestaDto>> Obtener(int id)
    {
        var turno = await Proyectar(Consultar().Where(x => x.Id == id)).SingleOrDefaultAsync();
        return turno is null ? NotFound() : Ok(turno);
    }

    [HttpPost]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Solicitar(TurnoDto dto)
    {
        if (dto.Id != 0) return BadRequest();
        var id = await turnos.GuardarAsync(dto, UsuarioId, false);
        return await RespuestaCreacion(id);
    }

    [HttpPost("presencial")]
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> ReservarPresencial(TurnoDto dto)
    {
        if (dto.Id != 0) return BadRequest();
        var id = await turnos.ReservarPresencialAsync(dto, UsuarioId);
        return await RespuestaCreacion(id);
    }

    private async Task<IActionResult> RespuestaCreacion(int id)
    {
        var creado = await Proyectar(Consultar().Where(x => x.Id == id)).SingleAsync();
        return CreatedAtAction(nameof(Obtener), new { id }, creado);
    }

    [HttpPost("{id:int}/confirmar")]
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Confirmar(int id, ConfirmarTurnoDto dto)
    {
        await turnos.CambiarEstadoAsync(id, EstadosTurno.Confirmado, UsuarioId, true, dto.Cantidad);
        return NoContent();
    }

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Roles = "Veterinario")]
    public Task<IActionResult> Rechazar(int id) => CambiarEstado(id, EstadosTurno.Rechazado);

    [HttpPost("{id:int}/completar")]
    [Authorize(Roles = "Veterinario")]
    public Task<IActionResult> Completar(int id) => CambiarEstado(id, EstadosTurno.Completado);

    [HttpPost("{id:int}/cancelar")]
    public Task<IActionResult> Cancelar(int id) =>
        CambiarEstado(id, EsVeterinario ? EstadosTurno.CanceladoVeterinario : EstadosTurno.CanceladoTutor);

    private async Task<IActionResult> CambiarEstado(int id, int destino)
    {
        await turnos.CambiarEstadoAsync(id, destino, UsuarioId, EsVeterinario);
        return NoContent();
    }
}
