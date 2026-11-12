using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = RolSesion.Administrador)]
public class AuditoriaApiController(AuditoriaService auditoria) : ControllerBase
{
    [HttpGet]
    public Task<AuditoriaPaginaDto> Listar([Range(1, 1000000)] int pagina = 1,
        [Range(1, int.MaxValue)] int? tutorId = null) => auditoria.ConsultarAsync(pagina, tutorId);
}
