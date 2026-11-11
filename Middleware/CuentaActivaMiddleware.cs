using Microsoft.AspNetCore.Identity;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Middleware;

public class CuentaActivaMiddleware
{
    private readonly RequestDelegate _next;
    public CuentaActivaMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> usuarios,
        SignInManager<ApplicationUser> sesiones)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var usuario = await usuarios.GetUserAsync(context.User);
            if (usuario is null || !usuario.Activo)
            {
                await CerrarAsync(context, sesiones);
                return;
            }

            var roles = (await usuarios.GetRolesAsync(usuario)).Where(RolSesion.Permitido).ToList();
            var elegido = context.User.FindFirst(RolSesion.Claim)?.Value;
            if (roles.Count == 0 || elegido is not null && !roles.Contains(elegido))
            {
                await CerrarAsync(context, sesiones);
                return;
            }
            if (elegido is not null || roles.Count == 1)
            {
                // La autorización y las vistas reciben únicamente el rol activo.
                RolSesion.Limitar(context.User, elegido ?? roles[0]);
            }
            else
            {
                RolSesion.Limitar(context.User, string.Empty);
                // No se habilita ningún rol hasta confirmar la elección por POST.
                var path = context.Request.Path;
                if (!path.StartsWithSegments("/Cuenta/SeleccionarRol")
                    && !path.StartsWithSegments("/Identity/Cuenta/Salir")
                    && !path.StartsWithSegments("/Identity/Cuenta/Ingresar")
                    && !path.StartsWithSegments("/Identity/Cuenta/AccesoDenegado"))
                {
                    if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new { mensaje = "Elegí el rol con el que vas a trabajar en esta sesión." });
                    }
                    else context.Response.Redirect("/Cuenta/SeleccionarRol");
                    return;
                }
            }
        }
        await _next(context);
    }

    private static async Task CerrarAsync(HttpContext context, SignInManager<ApplicationUser> sesiones)
    {
        await sesiones.SignOutAsync();
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { mensaje = "Tu sesión terminó. Iniciá sesión nuevamente." });
        }
        else context.Response.Redirect("/Identity/Cuenta/Ingresar");
    }
}
