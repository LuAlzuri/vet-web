using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/cupos")]
[Route("api/cupos-diarios")]
[Authorize(Roles = "Veterinario,Tutor")]
public class CuposController(ApplicationDbContext db, CupoService cupos) : ControllerBase
{
    [HttpGet]
    public Task<List<CupoDiarioDto>> Listar(bool incluirPasados = false)
    {
        var cupos = db.CuposDiarios.AsNoTracking();
        if (!incluirPasados || !User.IsInRole("Veterinario")) cupos = cupos.Where(x => x.FechaCupo >= Reloj.Hoy);
        if (!User.IsInRole("Veterinario"))
            cupos = cupos.Where(x => x.NumeroCuposDisponibles > 0);
        return cupos.OrderBy(x => x.FechaCupo)
            .Select(x => new CupoDiarioDto
            {
                Id = x.Id, FechaCupo = x.FechaCupo, NumeroCuposDisponibles = x.NumeroCuposDisponibles
            }).ToListAsync();
    }
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CupoDiarioDto>> Obtener(int id)
    {
        var dato=await db.CuposDiarios.AsNoTracking().Where(x => x.Id==id && (User.IsInRole("Veterinario") || x.FechaCupo>=Reloj.Hoy && x.NumeroCuposDisponibles>0))
            .Select(x => new CupoDiarioDto { Id=x.Id,FechaCupo=x.FechaCupo,NumeroCuposDisponibles=x.NumeroCuposDisponibles }).SingleOrDefaultAsync();
        return dato is null ? NotFound() : Ok(dato);
    }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Crear(CupoDiarioDto dto)
    {
        if(dto.Id!=0) return BadRequest();
        var id=await cupos.GuardarAsync(dto);
        return CreatedAtAction(nameof(Obtener),new { id },new CupoDiarioDto { Id=id!.Value,FechaCupo=dto.FechaCupo,NumeroCuposDisponibles=dto.NumeroCuposDisponibles });
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Modificar(int id,CupoDiarioDto dto)
    {
        if(dto.Id!=0 && dto.Id!=id) return BadRequest();
        dto.Id=id; return await cupos.GuardarAsync(dto) is null ? NotFound() : NoContent();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id) => await cupos.EliminarAsync(id) ? NoContent() : NotFound();

}
