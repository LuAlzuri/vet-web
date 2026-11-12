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
[ApiController]
public abstract class PerfilesApiController(ApplicationDbContext db, CuentaService cuentas, UserManager<ApplicationUser> usuarios) : ControllerBase
{
    protected abstract bool EsVeterinario { get; }
    private IQueryable<PerfilRespuestaDto> Consultar() => EsVeterinario
        ? db.Veterinarios.AsNoTracking().Where(x => x.Usuario.Activo).Select(x => new PerfilRespuestaDto { Id = x.Id, Nombre = x.Usuario.Nombre, Apellido = x.Usuario.Apellido, Email = x.Usuario.Email!, Matricula = x.Matricula })
        : db.Tutores.AsNoTracking().Where(x => x.Usuario.Activo).Select(x => new PerfilRespuestaDto { Id = x.Id, Nombre = x.Usuario.Nombre, Apellido = x.Usuario.Apellido, Email = x.Usuario.Email!, Matricula = null });
    [HttpGet]
    public Task<List<PerfilRespuestaDto>> Listar([Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20) =>
        Consultar().OrderBy(x => x.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PerfilRespuestaDto>> Obtener(int id)
    { var perfil = await Consultar().SingleOrDefaultAsync(x => x.Id == id); return perfil is null ? NotFound() : Ok(perfil); }
    [HttpPost]
    public async Task<IActionResult> Crear(CuentaDto dto)
    {
        if (dto.Id != 0) return BadRequest();
        var id = await cuentas.GuardarAsync(dto, EsVeterinario);
        return CreatedAtAction(nameof(Obtener), new { id }, await Consultar().SingleAsync(x => x.Id == id));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, CuentaDto dto)
    {
        if (dto.Id != 0 && dto.Id != id) return BadRequest();
        if (!await Consultar().AnyAsync(x => x.Id == id)) return NotFound();
        dto.Id = id; await cuentas.GuardarAsync(dto, EsVeterinario); return NoContent();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!await Consultar().AnyAsync(x => x.Id == id)) return NotFound();
        await cuentas.EliminarAsync(id, EsVeterinario, usuarios.GetUserId(User)!); return NoContent();
    }
}
[Route("api/tutores"), Authorize(Roles = "Veterinario")]
public class TutoresApiController(ApplicationDbContext db, CuentaService cuentas, UserManager<ApplicationUser> usuarios) : PerfilesApiController(db, cuentas, usuarios)
{ protected override bool EsVeterinario => false; }
[Route("api/veterinarios"), Authorize(Roles = RolSesion.Administrador)]
public class VeterinariosApiController(ApplicationDbContext db, CuentaService cuentas, UserManager<ApplicationUser> usuarios) : PerfilesApiController(db, cuentas, usuarios)
{ protected override bool EsVeterinario => true; }
