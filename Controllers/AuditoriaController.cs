using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Services;

namespace VetWeb.Controllers;

[Authorize(Roles = RolSesion.Administrador)]
public class AuditoriaController(AuditoriaService auditoria) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Listado(int pagina = 1, int? tutorId = null)
    {
        if (!ModelState.IsValid || pagina is < 1 or > 1000000 || tutorId is <= 0) return BadRequest();
        ViewBag.TutorId = tutorId;
        return View(await auditoria.ConsultarAsync(pagina, tutorId));
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Tabla(int draw, int start, int length, int? tutorId,
        [FromQuery(Name = "search[value]")] string? buscar,
        [FromQuery(Name = "order[0][dir]")] string? direccion)
    {
        if (!ModelState.IsValid || draw < 0 || start < 0 || length is < 1 or > 100 || tutorId is <= 0 ||
            buscar?.Length > 160) return BadRequest();

        var (total, filtrados, filas) = await auditoria.ConsultarTablaAsync(start, length, tutorId, buscar, direccion == "asc");
        return Json(new { draw, recordsTotal = total, recordsFiltered = filtrados, data = filas });
    }
}
