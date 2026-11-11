using System.Security.Claims;
namespace VetWeb.Services;

public static class RolSesion
{
    public const string Claim = "vetweb:rol-activo";
    public static bool Permitido(string rol) => rol == "Veterinario" || rol == "Tutor";

    public static void Limitar(ClaimsPrincipal principal, string rol)
    {
        foreach (var identidad in principal.Identities)
        {
            foreach (var claim in identidad.FindAll(identidad.RoleClaimType).Where(x => x.Value != rol).ToList())
                identidad.RemoveClaim(claim);
        }
        if (!string.IsNullOrEmpty(rol) && !principal.HasClaim(Claim, rol) && principal.Identity is ClaimsIdentity actual)
            actual.AddClaim(new Claim(Claim, rol));
    }
}
