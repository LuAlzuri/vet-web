using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers.Api;
[ApiController, Route("api/perros"), Authorize(Roles = "Veterinario,Tutor")]
public class PerrosApiController(ApplicationDbContext db, PerroService perros, TurnoService turnos, UserManager<ApplicationUser> usuarios) : ControllerBase
{
    private string UsuarioId => usuarios.GetUserId(User)!;
    private IQueryable<PerroRespuestaDto> Consultar() => perros.Visibles(UsuarioId, User.IsInRole("Veterinario"))
        .Select(x => new PerroRespuestaDto
        {
            Id = x.Id,
            Nombre = x.Nombre,
            FechaNacimiento = x.FechaNacimiento,
            TutorId = x.TutorId,
            GrupoEtarioId = x.GrupoEtarioId,
            TamanioId = x.TamanioId,
            Reactivo = x.Reactivo,
            CalendarioVacunasCompleto = x.CalendarioVacunasCompleto,
            TieneImagen = x.Imagen != null
        });
    [HttpGet]
    public Task<List<PerroRespuestaDto>> Listar([Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20, [Range(1, int.MaxValue)] int? tutorId = null) =>
        Consultar().Where(x => tutorId == null || x.TutorId == tutorId).OrderBy(x => x.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PerroRespuestaDto>> Obtener(int id)
    { var perro = await Consultar().SingleOrDefaultAsync(x => x.Id == id); return perro is null ? NotFound() : Ok(perro); }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Crear([FromBody] PerroDto dto)
    {
        if (dto.Id != 0 || dto.Foto is not null) return BadRequest();
        var id = await perros.GuardarAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, await Consultar().SingleAsync(x => x.Id == id));
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Modificar(int id, [FromBody] PerroDto dto)
    {
        if (dto.Id != 0 && dto.Id != id || dto.Foto is not null) return BadRequest();
        dto.Id = id; return await perros.GuardarAsync(dto) is null ? NotFound() : NoContent();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id) => await perros.EliminarAsync(id) ? NoContent() : NotFound();
    [HttpGet("{id:int}/historial")]
    public async Task<ActionResult<List<TurnoRespuestaDto>>> Historial(int id, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20)
    {
        if (!await db.Perros.IgnoreQueryFilters().AnyAsync(x => x.Id == id && (User.IsInRole("Veterinario") || x.Tutor.UserId == UsuarioId))) return NotFound();
        return await turnos.ConsultarVisibles(UsuarioId, User.IsInRole("Veterinario")).Where(x => x.PerroId == id)
            .OrderByDescending(x => x.CupoDiario.FechaCupo).ThenByDescending(x => x.Id).Skip((pagina - 1) * tamano).Take(tamano)
            .Select(x => new TurnoRespuestaDto(x.Id, x.PerroId, x.Perro.Nombre, x.CupoDiarioId, x.CupoDiario.FechaCupo, x.CantidadReservada, x.VeterinarioId, x.EstadoId, x.Estado.Descripcion, x.Observaciones)).ToListAsync();
    }
    [HttpGet("{id:int}/patologias")]
    public async Task<ActionResult<List<PatologiaPerroRespuestaDto>>> Patologias(int id)
    {
        if (!await perros.Visibles(UsuarioId, User.IsInRole("Veterinario")).AnyAsync(x => x.Id == id)) return NotFound();
        return await db.PerrosPatologias.AsNoTracking().Where(x => x.PerroId == id).OrderBy(x => x.PatologiaId)
            .Select(x => new PatologiaPerroRespuestaDto(x.PatologiaId, x.Patologia.Descripcion, x.Controlado, x.Observacion)).ToListAsync();
    }
}
