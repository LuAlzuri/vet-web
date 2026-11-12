using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;

internal static class CatalogosPruebas
{
    private static void Exigir(bool cumple, string mensaje)
    {
        if (!cumple) throw new Exception("Acceso a catálogos: " + mensaje);
    }

    public static async Task EjecutarAsync(ServiceProvider provider, HttpClient veterinario, HttpClient administrador,
        HttpClient tutor, HttpClient anonimo)
    {
        var catalogos = new[]
        {
            (Web: "Servicios", Api: "servicios"), (Web: "GruposEtarios", Api: "grupos-etarios"),
            (Web: "Tamanios", Api: "tamanios"), (Web: "Patologias", Api: "patologias"),
            (Web: "Tratamientos", Api: "tratamientos")
        };
        async Task<string> Token(HttpClient cliente, string ruta)
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Exigir(respuesta.IsSuccessStatusCode, "no abrió " + ruta);
            var html = await respuesta.Content.ReadAsStringAsync();
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Exigir(match.Success, "falta antifalsificación en " + ruta);
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Guardar(HttpClient cliente, string ruta, Dictionary<string,string> datos)
        {
            using var respuesta = await cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
            Exigir(respuesta.IsSuccessStatusCode && (await respuesta.Content.ReadAsStringAsync()).Contains("true"), "falló " + ruta);
        }
        foreach (var (cliente, rol) in new[] { (veterinario, "Veterinario"), (administrador, "Administrador") })
        {
            var inicio = await cliente.GetStringAsync("/");
            Exigir(inicio.Contains("/Catalogos/Listado"), "falta el acceso de " + rol);
            var pantalla = await cliente.GetStringAsync("/Catalogos/Listado");
            foreach (var catalogo in catalogos)
            {
                Exigir(pantalla.Contains($"/{catalogo.Web}/Listado"), "falta " + catalogo.Web);
                var listado = await cliente.GetStringAsync($"/{catalogo.Web}/Listado");
                Exigir(listado.Contains("/Catalogos/Listado"), "falta volver a catálogos");
                var token = await Token(cliente, $"/{catalogo.Web}/ObtenerFormulario");
                var descripcion = $"Catálogo web {rol} {catalogo.Web}";
                await Guardar(cliente, $"/{catalogo.Web}/Guardar", new()
                { ["__RequestVerificationToken"] = token, ["Descripcion"] = descripcion });
                var creado = (await cliente.GetFromJsonAsync<List<CatalogoDto>>($"/api/{catalogo.Api}"))!.Single(x => x.Descripcion == descripcion);
                await Guardar(cliente, $"/{catalogo.Web}/Guardar", new()
                { ["__RequestVerificationToken"] = token, ["Id"] = creado.Id.ToString(), ["Descripcion"] = descripcion + " editado" });
                var editado = await cliente.GetFromJsonAsync<CatalogoDto>($"/api/{catalogo.Api}/{creado.Id}");
                Exigir(editado!.Descripcion == descripcion + " editado", "no se guardó la edición de " + rol);
                await Guardar(cliente, $"/{catalogo.Web}/Eliminar?id={creado.Id}", new() { ["__RequestVerificationToken"] = token });
                using var eliminado = await cliente.GetAsync($"/api/{catalogo.Api}/{creado.Id}");
                Exigir(eliminado.StatusCode == HttpStatusCode.NotFound, "no se eliminó la opción");
                using var sinToken = await cliente.PostAsync($"/{catalogo.Web}/Guardar", new FormUrlEncodedContent(new Dictionary<string,string> { ["Descripcion"] = "Sin token" }));
                Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "se admitió una escritura sin antifalsificación");
            }
            Exigir(pantalla.Contains("/Estados/Listado"), "falta consulta de estados");
            using var estados = await cliente.GetAsync("/Estados/Listado");
            Exigir(estados.IsSuccessStatusCode, "no abrió estados para " + rol);
            var tokenEstados = await Token(cliente, "/Servicios/ObtenerFormulario");
            foreach (var ruta in new[] { "/Estados/Guardar", "/Estados/Eliminar?id=1" })
            {
                using var rechazo = await cliente.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenEstados }));
                Exigir(rechazo.StatusCode == HttpStatusCode.Redirect && rechazo.Headers.Location!.ToString().Contains("AccesoDenegado"), "se habilitó edición de estados");
            }
        }
        foreach (var ruta in catalogos.Select(x => $"/{x.Web}/Listado").Append("/Catalogos/Listado").Append("/Estados/Listado"))
        {
            using var rechazo = await tutor.GetAsync(ruta);
            Exigir(rechazo.StatusCode == HttpStatusCode.Redirect && rechazo.Headers.Location!.ToString().Contains("AccesoDenegado"), "Tutor accedió a " + ruta);
        }
        Exigir(!(await tutor.GetStringAsync("/")).Contains("/Catalogos/Listado"), "Tutor ve el acceso de gestión");
        using var sinSesion = await anonimo.GetAsync("/Catalogos/Listado");
        Exigir(sinSesion.StatusCode == HttpStatusCode.Redirect && sinSesion.Headers.Location!.ToString().Contains("Ingresar"), "anónimo accedió a gestión");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var grupoUsado = await db.Perros.Select(x => x.GrupoEtarioId).FirstAsync();
            var token = await Token(administrador, "/GruposEtarios/ObtenerFormulario");
            using var rechazo = await administrador.PostAsync($"/GruposEtarios/Eliminar?id={grupoUsado}", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token }));
            Exigir(rechazo.StatusCode == HttpStatusCode.BadRequest && await db.GruposEtarios.AnyAsync(x => x.Id == grupoUsado), "se eliminó un catálogo referenciado");
        }
        Console.WriteLine("Catálogos: acceso e inicio para Veterinario y Administrador, CRUD web/API, navegación de regreso, antifalsificación, referencias y estados inmutables verificados.");
    }
}
