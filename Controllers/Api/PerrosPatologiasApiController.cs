using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Services;
namespace VetWeb.Controllers.Api;
[ApiController, Route("api/perros/{perroId:int}/patologias"), Authorize(Roles = "Veterinario")]
public class PerrosPatologiasApiController(ApplicationDbContext db, PerroPatologiaService patologias) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Crear(int perroId, PatologiaSolicitudDto dto)
    {
        var patologiaId = await patologias.GuardarAsync(new PerroPatologiaDto { PerroId = perroId, PatologiaId = dto.PatologiaId, Controlado = dto.Controlado, Observacion = dto.Observacion, NuevaPatologia = dto.NuevaPatologia });
        return Created($"/api/perros/{perroId}/patologias", new PatologiaPerroRespuestaDto(patologiaId!.Value, await db.Patologias.Where(x => x.Id == patologiaId).Select(x => x.Descripcion).SingleAsync(), dto.Controlado, dto.Observacion?.Trim()));
    }
    [HttpPut("{patologiaId:int}")]
    public async Task<IActionResult> Modificar(int perroId, int patologiaId, PatologiaSolicitudDto dto)
    {
        if (dto.PatologiaId != 0 && dto.PatologiaId != patologiaId) return BadRequest();
        return await patologias.GuardarAsync(new PerroPatologiaDto { EsEdicion = true, PerroId = perroId, PatologiaId = patologiaId, Controlado = dto.Controlado, Observacion = dto.Observacion }) is not null ? NoContent() : NotFound();
    }
    [HttpDelete("{patologiaId:int}")]
    public async Task<IActionResult> Eliminar(int perroId, int patologiaId) => await patologias.EliminarAsync(perroId, patologiaId) ? NoContent() : NotFound();
}
