using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Models;
using VetWeb.Services;

internal static class PerfilesAdministracionPruebas
{
    public static async Task EjecutarAsync(IServiceProvider provider, HttpClient admin, HttpClient vet,
        string tokenAdmin, string tokenVet, string password)
    {
        static void Comprobar(bool valido, string mensaje) { if (!valido) throw new Exception(mensaje); }
        async Task<HttpResponseMessage> Guardar(HttpClient cliente, Dictionary<string, string> campos, string token)
        {
            campos["__RequestVerificationToken"] = token;
            return await cliente.PostAsync("/Veterinarios/Guardar", new FormUrlEncodedContent(campos));
        }
        var formulario = await admin.GetStringAsync("/Veterinarios/ObtenerFormulario");
        Comprobar(formulario.Contains("data-cliente") && formulario.Contains("EsAdministrador") && formulario.Contains("data-es-cliente"),
            "El formulario no ofrece la selección de cliente y los roles.");
        int clienteId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            clienteId = await db.Tutores.Where(x => x.Usuario.Email == "otro@prueba.invalid").Select(x => x.Id).SingleAsync();
        }
        var vinculo = new Dictionary<string, string> { ["VincularExistente"] = "true", ["ClienteId"] = clienteId.ToString(),
            ["Nombre"] = "Manipulado", ["Apellido"] = "Manipulado", ["Email"] = "ignorado@prueba.invalid", ["Matricula"] = "98760" };
        using var vinculado = await Guardar(admin, vinculo, tokenAdmin);
        Comprobar(vinculado.IsSuccessStatusCode && (await vinculado.Content.ReadAsStringAsync()).Contains("\"success\":true"), "No se pudo seleccionar un cliente existente.");
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await users.FindByEmailAsync("otro@prueba.invalid"))!;
            Comprobar(usuario.Nombre == "Otro" && await users.CheckPasswordAsync(usuario, password) && await users.IsInRoleAsync(usuario, "Veterinario"),
                "La vinculación modificó datos bloqueados o no asignó el rol.");
        }
        var datos = new Dictionary<string, string> { ["Nombre"] = "Cuenta", ["Apellido"] = "Completa", ["Email"] = "completa@prueba.invalid",
            ["Password"] = password, ["Matricula"] = "98761", ["EsCliente"] = "true", ["EsAdministrador"] = "true", ["Telefono"] = "+54 11 2233 4455" };
        using var prohibido = await Guardar(vet, datos, tokenVet);
        Comprobar(prohibido.StatusCode == HttpStatusCode.Redirect, "Un veterinario pudo otorgar el rol Administrador.");
        using var creado = await Guardar(admin, datos, tokenAdmin);
        Comprobar(creado.IsSuccessStatusCode && (await creado.Content.ReadAsStringAsync()).Contains("\"success\":true"), "Falló el alta con tres roles.");
        int id;
        string userId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await users.FindByEmailAsync(datos["Email"]))!;
            userId = usuario.Id;
            id = await db.Veterinarios.Where(x => x.UserId == usuario.Id).Select(x => x.Id).SingleAsync();
            var tutor = await db.Tutores.Include(x => x.Telefonos).SingleAsync(x => x.UserId == usuario.Id);
            Comprobar((await users.GetRolesAsync(usuario)).Count == 3 && tutor.Telefonos.Single().Numero == datos["Telefono"], "No se guardaron los tres roles y el teléfono.");
            var servicio = scope.ServiceProvider.GetRequiredService<CuentaService>();
            try { await servicio.EliminarAsync(id, true, userId); throw new Exception("Se permitió la baja propia."); }
            catch (InvalidOperationException) { }
            try
            {
                await servicio.GuardarAsync(new() { Id = id, Nombre = usuario.Nombre, Apellido = usuario.Apellido,
                    Email = usuario.Email!, Matricula = 98761, EsAdministrador = false }, true, userId);
                throw new Exception("Se permitió quitar el rol administrativo propio.");
            }
            catch (InvalidOperationException) { }
        }
        datos["Id"] = id.ToString(); datos["EsAdministrador"] = "false";
        using var retiro = await Guardar(admin, datos, tokenAdmin);
        Comprobar(retiro.IsSuccessStatusCode && (await retiro.Content.ReadAsStringAsync()).Contains("\"success\":true"), "Otro administrador no pudo retirar el rol.");
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await users.FindByIdAsync(userId))!;
            Comprobar(!await users.IsInRoleAsync(usuario, RolSesion.Administrador) && (await users.GetUsersInRoleAsync(RolSesion.Administrador)).Any(x => x.Activo),
                "No se conservó al menos un administrador activo.");
        }
        Console.WriteLine("Perfiles administrativos: selección de cliente, datos inmutables, tres roles, teléfono, permisos y protección de administrador propio verificados.");
    }
}
