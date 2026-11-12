using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Controllers;

[Authorize(Roles = "Veterinario")]
public class VetosController(ApplicationDbContext db, VetoService vetos, UserManager<ApplicationUser> usuarios) : Controller
{
    public async Task<IActionResult> Listado(int tutorId)
    {
        var nombre = await db.Tutores.Where(x => x.Id == tutorId && x.Usuario.Activo)
            .Select(x => x.Usuario.Nombre + " " + x.Usuario.Apellido).SingleOrDefaultAsync();
        if (nombre is null) return NotFound();
        ViewBag.TutorId = tutorId;
        ViewBag.Tutor = nombre;
        return View(await vetos.ConsultarAsync(tutorId));
    }

    [HttpGet]
    public IActionResult ObtenerFormulario(int tutorId, int? vetoId) => Formulario(tutorId, vetoId, new());

    [HttpPost]
    public async Task<IActionResult> Guardar(int tutorId, int? vetoId, MotivoVetoDto dto)
    {
        if (!ModelState.IsValid) return Formulario(tutorId, vetoId, dto);
        var usuarioId = usuarios.GetUserId(User)!;
        if (vetoId.HasValue)
            await vetos.LevantarAsync(tutorId, vetoId.Value, usuarioId, dto.Motivo);
        else
            await vetos.AplicarAsync(tutorId, usuarioId, dto.Motivo);
        return Json(new { success = true });
    }

    private IActionResult Formulario(int tutorId, int? vetoId, MotivoVetoDto dto)
    {
        ViewBag.TutorId = tutorId;
        ViewBag.VetoId = vetoId;
        return PartialView("_Formulario", dto);
    }
}
