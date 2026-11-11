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
public class VeterinariosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CuentaService _cuentas;
    private readonly UserManager<ApplicationUser> _usuarios;
    public VeterinariosController(ApplicationDbContext context, CuentaService cuentas, UserManager<ApplicationUser> usuarios)
    { _context = context; _cuentas = cuentas; _usuarios = usuarios; }
    private void Preparar() { ViewBag.Controlador = "Veterinarios"; ViewBag.Titulo = "Veterinarios"; ViewBag.EsVeterinario = true; }
    public async Task<IActionResult> Listado()
    {
        Preparar();
        var datos = await _context.Veterinarios.AsNoTracking().Where(x => x.Usuario.Activo).OrderBy(x => x.Usuario.Apellido)
            .Select(x => new CuentaDto { Id = x.Id, Nombre = x.Usuario.Nombre, Apellido = x.Usuario.Apellido, Email = x.Usuario.Email!, Matricula = x.Matricula, }).ToListAsync();
        return View("~/Views/Shared/Cuentas.cshtml", datos);
    }
    public async Task<IActionResult> ObtenerFormulario(int id = 0)
    {
        Preparar();
        if (id == 0) return PartialView("~/Views/Shared/_FormularioCuenta.cshtml", new CuentaDto());
        var dto = await _context.Veterinarios.Where(x => x.Id == id && x.Usuario.Activo)
            .Select(x => new CuentaDto { Id = x.Id, Nombre = x.Usuario.Nombre, Apellido = x.Usuario.Apellido, Email = x.Usuario.Email!, Matricula = x.Matricula, }).SingleOrDefaultAsync();
        return dto is null ? NotFound() : PartialView("~/Views/Shared/_FormularioCuenta.cshtml", dto);
    }
    [HttpPost]
    public async Task<IActionResult> Guardar(CuentaDto dto)
    {
        Preparar();
        if (!ModelState.IsValid) return PartialView("~/Views/Shared/_FormularioCuenta.cshtml", dto);
        await _cuentas.GuardarAsync(dto, true);
        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _cuentas.EliminarAsync(id, true, _usuarios.GetUserId(User)!);
        return Json(new { success = true });
    }
}
