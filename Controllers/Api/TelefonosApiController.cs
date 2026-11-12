using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Services;
namespace VetWeb.Controllers.Api;
[ApiController, Route("api/tutores/{tutorId:int}/telefonos"), Authorize(Roles = "Veterinario")]
public class TelefonosApiController(ApplicationDbContext db, TelefonoTutorService telefonos) : ControllerBase
{
    private IQueryable<TelefonoTutorDto> Consultar(int tutorId) => db.TelefonosTutores.AsNoTracking().Where(x => x.TutorId == tutorId)
        .Select(x => new TelefonoTutorDto { Id = x.Id, TutorId = x.TutorId, Numero = x.Numero, NombrePropietario = x.NombrePropietario });
    [HttpGet]
    public async Task<ActionResult<List<TelefonoTutorDto>>> Listar(int tutorId)
    {
        if (!await db.Tutores.AnyAsync(x => x.Id == tutorId && x.Usuario.Activo)) return NotFound();
        return await Consultar(tutorId).OrderBy(x => x.Id).ToListAsync();
    }
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TelefonoTutorDto>> Obtener(int tutorId, int id)
    { var dato = await Consultar(tutorId).SingleOrDefaultAsync(x => x.Id == id); return dato is null ? NotFound() : Ok(dato); }
    [HttpPost]
    public async Task<IActionResult> Crear(int tutorId, TelefonoSolicitudDto dto)
    {
        var id = await telefonos.GuardarAsync(new TelefonoTutorDto { TutorId = tutorId, NombrePropietario = dto.NombrePropietario, Numero = dto.Numero });
        return CreatedAtAction(nameof(Obtener), new { tutorId, id }, await Consultar(tutorId).SingleAsync(x => x.Id == id));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int tutorId, int id, TelefonoSolicitudDto dto) =>
        await telefonos.GuardarAsync(new TelefonoTutorDto { Id = id, TutorId = tutorId, NombrePropietario = dto.NombrePropietario, Numero = dto.Numero }, tutorId) is null ? NotFound() : NoContent();
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int tutorId, int id) => await telefonos.EliminarAsync(id, tutorId) ? NoContent() : NotFound();
}
