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
[Route("api/tutores/{tutorId:int}/vetos")]
[Authorize(Roles = "Veterinario")]
public class VetosApiController(ApplicationDbContext db, VetoService vetos, UserManager<ApplicationUser> usuarios) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VetoDto>>> Listar(int tutorId)
    {
        if (!await db.Tutores.AnyAsync(x => x.Id == tutorId && x.Usuario.Activo)) return NotFound();
        return Ok(await vetos.ConsultarAsync(tutorId));
    }

    [HttpGet("{vetoId:int}")]
    public async Task<ActionResult<VetoDto>> Obtener(int tutorId, int vetoId)
    {
        if (!await db.Tutores.AnyAsync(x => x.Id == tutorId && x.Usuario.Activo)) return NotFound();
        var veto = (await vetos.ConsultarAsync(tutorId)).SingleOrDefault(x => x.Id == vetoId);
        return veto is null ? NotFound() : Ok(veto);
    }

    [HttpPost]
    public async Task<IActionResult> Aplicar(int tutorId, MotivoVetoDto dto)
    {
        var id = await vetos.AplicarAsync(tutorId, usuarios.GetUserId(User)!, dto.Motivo);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPost("{vetoId:int}/levantar")]
    public async Task<IActionResult> Levantar(int tutorId, int vetoId, MotivoVetoDto dto)
    {
        await vetos.LevantarAsync(tutorId, vetoId, usuarios.GetUserId(User)!, dto.Motivo);
        return NoContent();
    }
}
