using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Controllers;

[Authorize]
public class CuentaController : Controller
{
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly SignInManager<ApplicationUser> _sesiones;
    public CuentaController(UserManager<ApplicationUser> usuarios, SignInManager<ApplicationUser> sesiones)
    { _usuarios = usuarios; _sesiones = sesiones; }

    [HttpPost]
    public async Task<IActionResult> CambiarRol()
    {
        var usuario = await _usuarios.GetUserAsync(User);
        if (usuario is null || !usuario.Activo) return Challenge();
        var roles = (await _usuarios.GetRolesAsync(usuario)).Count(RolSesion.Permitido);
        if (roles < 2) return Forbid();
        var autenticacion = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        // Renovar la autenticación sin una elección activa. La siguiente petición
        // solo podrá acceder al selector hasta confirmar uno de sus roles.
        await _sesiones.SignInAsync(usuario, autenticacion.Properties ?? new AuthenticationProperties());
        return LocalRedirect("/Cuenta/SeleccionarRol");
    }

    [HttpGet]
    public async Task<IActionResult> SeleccionarRol()
    {
        if (User.FindFirst(RolSesion.Claim) is not null) return LocalRedirect("/");
        var usuario = await _usuarios.GetUserAsync(User);
        if (usuario is null || !usuario.Activo) return Challenge();
        var roles = (await _usuarios.GetRolesAsync(usuario)).Where(RolSesion.Permitido).ToList();
        return View(roles);
    }

    [HttpPost]
    public async Task<IActionResult> SeleccionarRol(string rol)
    {
        // La elección cambia únicamente pasando por CambiarRol y este selector.
        if (User.FindFirst(RolSesion.Claim) is not null) return Forbid();
        var usuario = await _usuarios.GetUserAsync(User);
        if (usuario is null || !usuario.Activo) return Challenge();
        if (!RolSesion.Permitido(rol) || !await _usuarios.IsInRoleAsync(usuario, rol)) return Forbid();
        var autenticacion = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        await _sesiones.SignInWithClaimsAsync(usuario, autenticacion.Properties ?? new AuthenticationProperties(),
            new[] { new Claim(RolSesion.Claim, rol) });
        return LocalRedirect("/");
    }
}
