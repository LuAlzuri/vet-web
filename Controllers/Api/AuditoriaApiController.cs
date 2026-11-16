using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = RolSesion.Administrador)]
public class AuditoriaApiController(AuditoriaService auditoria, ConsultaErroresService errores) : ControllerBase
{
    [HttpGet]
    public Task<AuditoriaPaginaDto> Listar([Range(1, 1000000)] int pagina = 1,
        [Range(1, int.MaxValue)] int? tutorId = null) => auditoria.ConsultarAsync(pagina, tutorId);

    [HttpGet("errores")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<List<ErrorLogRespuesta>>> ListarErrores(DateOnly? desde, DateOnly? hasta,
        [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20, CancellationToken ct = default)
    {
        if (desde > hasta) return BadRequest(new { mensaje = "La fecha inicial no puede ser posterior a la fecha final." });
        try { return await errores.ListarAsync(desde, hasta, pagina, tamano, ct); }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = "No se pudo leer el registro de errores." });
        }
    }
}
