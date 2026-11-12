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
}
