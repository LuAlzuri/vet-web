using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

// Estas pruebas crean y eliminan únicamente una base temporal de nombre aleatorio.
// No aplican migraciones ni escriben datos en la base configurada por el usuario.
var config = new ConfigurationBuilder().AddUserSecrets("VetWeb-Desarrollo-2026").Build();
var original = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
var temporal = "vetweb_prueba_" + Guid.NewGuid().ToString("N");
var directorioFotosPrueba = Path.Combine(Path.GetTempPath(), temporal + "_fotos");
var admin = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = "postgres", Pooling = false };
var prueba = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = temporal };
await using var conexionAdmin = new NpgsqlConnection(admin.ConnectionString);
await conexionAdmin.OpenAsync();
await using (var crear = new NpgsqlCommand($"CREATE DATABASE \"{temporal}\"", conexionAdmin)) await crear.ExecuteNonQueryAsync();
Process? servidor = null;
var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
void Verificar(bool condicion, string mensaje) { if (!condicion) throw new Exception(mensaje); }
async Task Rechazar(Func<Task> accion, string mensaje)
{
    try { await accion(); } catch (InvalidOperationException) { return; }
    throw new Exception(mensaje);
}
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(prueba.ConnectionString));
services.AddIdentityCore<ApplicationUser>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 8; })
    .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
services.AddScoped<CuentaService>(); services.AddScoped<TurnoService>(); services.AddScoped<CatalogosFormularioService>();
await using var provider = services.BuildServiceProvider();
string vetId = "", tutorId = "", otroId = "", dualId = "";
int perroId = 0, otroPerroId = 0, cupoId = 0, servicioId = 0, vetPerfil = 0, dualPerfil = 0, turnoId = 0;
try
{
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        Verificar(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Quedaron migraciones pendientes.");
        var cuentas = scope.ServiceProvider.GetRequiredService<CuentaService>();
        await cuentas.GuardarAsync(new CuentaDto { Nombre = "Vet", Apellido = "Prueba", Email = "vet@prueba.invalid", Password = password, Matricula = 12345 }, true);
        await cuentas.GuardarAsync(new CuentaDto { Nombre = "Tutor", Apellido = "Prueba", Email = "tutor@prueba.invalid", Password = password }, false);
        await cuentas.GuardarAsync(new CuentaDto { Nombre = "Otro", Apellido = "Prueba", Email = "otro@prueba.invalid", Password = password }, false);
        await cuentas.GuardarAsync(new CuentaDto { Nombre = "Dual", Apellido = "Prueba", Email = "dual@prueba.invalid", Password = password }, false);
        await cuentas.GuardarAsync(new CuentaDto { Nombre = "No cambia", Apellido = "No cambia", Email = "dual@prueba.invalid", VincularExistente = true, Matricula = 54321 }, true);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        vetId = (await users.FindByEmailAsync("vet@prueba.invalid"))!.Id;
        tutorId = (await users.FindByEmailAsync("tutor@prueba.invalid"))!.Id;
        otroId = (await users.FindByEmailAsync("otro@prueba.invalid"))!.Id;
        var dual = (await users.FindByEmailAsync("dual@prueba.invalid"))!; dualId = dual.Id;
        Verificar(dual.Nombre == "Dual" && await users.CheckPasswordAsync(dual, password), "Vincular perfiles cambió datos o contraseña.");
        Verificar((await users.GetRolesAsync(dual)).Count == 2, "No se vincularon ambos roles.");
        vetPerfil = (await db.Veterinarios.SingleAsync(x => x.UserId == vetId)).Id;
        dualPerfil = (await db.Tutores.SingleAsync(x => x.UserId == dualId)).Id;
        await Rechazar(() => cuentas.EliminarAsync(vetPerfil, true, vetId), "Se permitió eliminar la cuenta propia.");
        var grupo = new GrupoEtario { Descripcion = "Adulto de prueba" }; var tamanio = new Tamanio { Descripcion = "Mediano de prueba" };
        var servicio = new Servicio { Descripcion = "Baño de prueba" };
        db.AddRange(grupo, tamanio, servicio); await db.SaveChangesAsync(); servicioId = servicio.Id;
        var tutor = await db.Tutores.SingleAsync(x => x.UserId == tutorId);
        var otro = await db.Tutores.SingleAsync(x => x.UserId == otroId);
        var perro = new Perro { Nombre = "Perro prueba", FechaNacimiento = Reloj.Hoy.AddYears(-2), TutorId = tutor.Id, GrupoEtarioId = grupo.Id, TamanioId = tamanio.Id };
        var otroPerro = new Perro { Nombre = "Otro perro", FechaNacimiento = Reloj.Hoy.AddYears(-2), TutorId = otro.Id, GrupoEtarioId = grupo.Id, TamanioId = tamanio.Id };
        var cupo = new CupoDiario { FechaCupo = Reloj.Hoy.AddDays(1), NumeroCuposDisponibles = 1 };
        db.AddRange(perro, otroPerro, cupo); await db.SaveChangesAsync(); perroId = perro.Id; otroPerroId = otroPerro.Id; cupoId = cupo.Id;
        var turnos = scope.ServiceProvider.GetRequiredService<TurnoService>();
        TurnoDto Solicitud(int id) => new() { PerroId = id, VeterinarioId = vetPerfil, CupoDiarioId = cupoId, ServicioIds = new() { servicioId } };
        await Rechazar(() => turnos.GuardarAsync(Solicitud(perroId), otroId, false), "Se reservó un perro ajeno.");
        await turnos.GuardarAsync(Solicitud(perroId), tutorId, false);
        turnoId = (await db.Turnos.SingleAsync()).Id;
        Verificar((await db.CuposDiarios.SingleAsync()).NumeroCuposDisponibles == 0, "No se consumió el cupo.");
        await Rechazar(() => turnos.GuardarAsync(Solicitud(otroPerroId), otroId, false), "Se sobrevendió el cupo.");
        await Rechazar(() => turnos.CambiarEstadoAsync(turnoId, EstadosTurno.CanceladoTutor, otroId, false), "Se canceló un turno ajeno.");
    }
    // Contextos nuevos para comprobar que los rechazos no dejaron cambios persistidos.
    await using (var scope = provider.CreateAsyncScope())
    {
        var servicio = scope.ServiceProvider.GetRequiredService<TurnoService>();
        await servicio.CambiarEstadoAsync(turnoId, EstadosTurno.CanceladoTutor, tutorId, false);
        await Rechazar(() => servicio.CambiarEstadoAsync(turnoId, EstadosTurno.CanceladoTutor, tutorId, false), "Se devolvió dos veces un cupo.");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Verificar((await db.CuposDiarios.SingleAsync()).NumeroCuposDisponibles == 1, "La devolución del cupo es incorrecta.");
    }
    // Dos solicitudes simultáneas por la última plaza deben dejar exactamente una reserva.
    async Task<bool> Reservar(int perro, string usuario)
    {
        await using var scope = provider.CreateAsyncScope();
        try { await scope.ServiceProvider.GetRequiredService<TurnoService>().GuardarAsync(new TurnoDto { PerroId = perro, VeterinarioId = vetPerfil, CupoDiarioId = cupoId, ServicioIds = new() { servicioId } }, usuario, false); return true; }
        catch (InvalidOperationException) { return false; }
        catch (DbUpdateException) { return false; }
        catch (PostgresException ex) when (ex.SqlState == "40001") { return false; }
    }
    var reservas = await Task.WhenAll(Reservar(perroId, tutorId), Reservar(otroPerroId, otroId));
    Verificar(reservas.Count(x => x) == 1, "La reserva concurrente no dejó exactamente un ganador.");
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Verificar((await db.CuposDiarios.SingleAsync()).NumeroCuposDisponibles == 0, "Disponibilidad incorrecta tras concurrencia.");
    }
    Console.WriteLine("Servicios: altas de cuentas, dos roles, protección de baja propia, pertenencia, reserva, devolución única y concurrencia correctas.");

    // Servidor real contra la base temporal: cookies, Razor, formularios y permisos.
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    var dll = Path.Combine(root, "bin", "VerificacionIdentity", "net8.0", "VetWeb.dll");
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(dll); start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://localhost:5057");
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ConnectionStrings__DefaultConnection"] = prueba.ConnectionString;
    start.Environment["FotosPerros__Directorio"] = directorioFotosPrueba;
    servidor = Process.Start(start)!;
    servidor.BeginOutputReadLine(); servidor.BeginErrorReadLine();
    HttpClient Cliente() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri("http://localhost:5057") };
    using var anon = Cliente();
    for (var i = 0; i < 40; i++) { try { using var response = await anon.GetAsync("/"); if (response.IsSuccessStatusCode) break; } catch (HttpRequestException) { } await Task.Delay(250); }
    async Task<string> Token(HttpClient client, string url)
    {
        var response = await client.GetAsync(url); var html = await response.Content.ReadAsStringAsync();
        Verificar(response.StatusCode == HttpStatusCode.OK, "Falló GET " + url + " (" + response.StatusCode + ")");
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Verificar(match.Success, "Falta token en " + url); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    async Task Ingresar(HttpClient client, string email, string? clave = null)
    {
        var token = await Token(client, "/Identity/Cuenta/Ingresar");
        var response = await client.PostAsync("/Identity/Cuenta/Ingresar", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["Input.Email"] = email, ["Input.Password"] = clave ?? password }));
        Verificar(response.StatusCode == HttpStatusCode.Redirect, "No se pudo iniciar sesión de prueba.");
    }
    using var vet = Cliente(); using var tutorCliente = Cliente(); using var dualCliente = Cliente();
    await Ingresar(vet, "vet@prueba.invalid"); await Ingresar(tutorCliente, "tutor@prueba.invalid"); await Ingresar(dualCliente, "dual@prueba.invalid");
    foreach(var controller in new[]{"Tutores","Veterinarios","Perros","TelefonosTutores","PerrosPatologias","GruposEtarios","Tamanios","Patologias","Estados","Tratamientos","Servicios","CuposDiarios","Turnos"})
    {
        using var response = await vet.GetAsync($"/{controller}/Listado");
        Verificar(response.StatusCode == HttpStatusCode.OK, "Falló listado " + controller);
        using var formulario = await vet.GetAsync($"/{controller}/ObtenerFormulario" + (controller == "PerrosPatologias" ? "?perroId=" + perroId : ""));
        if (controller == "Estados")
            Verificar(formulario.StatusCode == HttpStatusCode.Redirect && formulario.Headers.Location!.ToString().Contains("AccesoDenegado"), "Estados permite abrir un formulario de modificación.");
        else Verificar(formulario.StatusCode == HttpStatusCode.OK, "Falló formulario " + controller);
    }
    var prohibido = await tutorCliente.GetAsync("/Tutores/Listado");
    Verificar(prohibido.StatusCode == HttpStatusCode.Redirect && prohibido.Headers.Location!.ToString().Contains("AccesoDenegado"), "El tutor accede a administración.");
    var registro = await anon.GetAsync("/Identity/Account/Register"); Verificar(registro.StatusCode == HttpStatusCode.NotFound, "Registro público habilitado.");
    var sinToken = await vet.PostAsync("/Servicios/Guardar", new FormUrlEncodedContent(new Dictionary<string,string>{{"Descripcion","Sin token"}}));
    Verificar(sinToken.StatusCode == HttpStatusCode.BadRequest, "Se aceptó un POST sin token.");
    using(var libreria = await anon.GetAsync("/lib/sweetalert2/sweetalert2.all.min.js")) Verificar(libreria.IsSuccessStatusCode, "SweetAlert2 no se sirve localmente.");
    using(var estilos = await anon.GetAsync("/lib/sweetalert2/sweetalert2.min.css")) Verificar(estilos.IsSuccessStatusCode, "Faltan los estilos de SweetAlert2.");
    var tokenCatalogo = await Token(vet, "/Servicios/ObtenerFormulario");
    var altaCatalogo = await vet.PostAsync("/Servicios/Guardar", new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",tokenCatalogo},{"Descripcion","Servicio desde formulario"}}));
    Verificar(altaCatalogo.IsSuccessStatusCode && (await altaCatalogo.Content.ReadAsStringAsync()).Contains("true"), "Falló alta AJAX de catálogo.");
    var altaEstado = await vet.PostAsync("/Estados/Guardar", new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",tokenCatalogo},{"Descripcion","Estado adicional de prueba"}}));
    Verificar(altaEstado.StatusCode == HttpStatusCode.Redirect && altaEstado.Headers.Location!.ToString().Contains("AccesoDenegado"), "Se permitió agregar un estado.");
    foreach (var rutaEstado in new[] { "/Estados/Guardar?id=1", "/Estados/Eliminar?id=1" })
    {
        using var bloqueadoEstado = await vet.PostAsync(rutaEstado, new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenCatalogo, ["Descripcion"] = "Modificado" }));
        Verificar(bloqueadoEstado.StatusCode == HttpStatusCode.Redirect && bloqueadoEstado.Headers.Location!.ToString().Contains("AccesoDenegado"), "Se permitió modificar estados fijos.");
    }
    async Task<HttpResponseMessage> Enviar(string ruta, Dictionary<string,string> campos)
    {
        if (ruta.StartsWith("/PerrosPatologias/Guardar") && !campos.ContainsKey("ContextoPerro"))
        {
            var formularioContexto = await (await vet.GetAsync("/PerrosPatologias/ObtenerFormulario?perroId=" + campos["PerroId"])).Content.ReadAsStringAsync();
            campos["ContextoPerro"] = WebUtility.HtmlDecode(Regex.Match(formularioContexto, "name=\"ContextoPerro\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        campos["__RequestVerificationToken"] = tokenCatalogo;
        return await vet.PostAsync(ruta, new FormUrlEncodedContent(campos));
    }
    async Task Exito(string ruta, Dictionary<string,string> campos)
    {
        using var response = await Enviar(ruta, campos);
        var body = await response.Content.ReadAsStringAsync();
        Verificar(response.IsSuccessStatusCode && body.Contains("\"success\":true"), "Falló POST " + ruta + " (" + response.StatusCode + "): " + body[..Math.Min(body.Length, 300)]);
    }
    using(var invalido = await Enviar("/Servicios/Guardar", new() { ["Descripcion"] = "" }))
        Verificar(invalido.IsSuccessStatusCode && (await invalido.Content.ReadAsStringAsync()).Contains("field-validation-error"), "El formulario vacío no devolvió sus errores.");
    int servicioHttp;
    await using(var scope = provider.CreateAsyncScope())
        servicioHttp = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Servicios.SingleAsync(x => x.Descripcion == "Servicio desde formulario")).Id;
    await Exito("/Servicios/Guardar", new() { ["Id"] = servicioHttp.ToString(), ["Descripcion"] = "Servicio editado" });
    await Exito("/Servicios/Eliminar?id=" + servicioHttp, new());
    await Exito("/Tutores/Guardar", new() { ["Nombre"] = "HTTP", ["Apellido"] = "Prueba", ["Email"] = "http@prueba.invalid", ["Password"] = password });
    int tutorHttp;
    int grupoHttp, tamanioHttp;
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        tutorHttp = (await db.Tutores.Include(x=>x.Usuario).SingleAsync(x => x.Usuario.Email == "http@prueba.invalid")).Id;
        grupoHttp = (await db.GruposEtarios.FirstAsync()).Id; tamanioHttp = (await db.Tamanios.FirstAsync()).Id;
    }
    await Exito("/Tutores/Guardar", new() { ["Id"] = tutorHttp.ToString(), ["Nombre"] = "HTTP editado", ["Apellido"] = "Prueba", ["Email"] = "http@prueba.invalid" });
    await Exito("/TelefonosTutores/Guardar", new() { ["TutorId"] = tutorHttp.ToString(), ["NombrePropietario"] = "Contacto", ["Numero"] = "+54 11 1234 5678" });
    int telefonoHttp;
    await using(var scope = provider.CreateAsyncScope()) telefonoHttp = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TelefonosTutores.SingleAsync()).Id;
    await Exito("/TelefonosTutores/Guardar", new() { ["Id"] = telefonoHttp.ToString(), ["TutorId"] = tutorHttp.ToString(), ["NombrePropietario"] = "Contacto editado", ["Numero"] = "+54 11 9876 5432" });
    await Exito("/TelefonosTutores/Eliminar?id=" + telefonoHttp, new());
    var fecha = Reloj.Hoy.AddYears(-3).ToString("yyyy-MM-dd");
    await Exito("/Perros/Guardar", new() { ["Nombre"] = "Perro HTTP", ["FechaNacimiento"] = fecha, ["TutorId"] = tutorHttp.ToString(), ["GrupoEtarioId"] = grupoHttp.ToString(), ["TamanioId"] = tamanioHttp.ToString() });
    int perroHttp, patologiaHttp;
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        perroHttp = (await db.Perros.SingleAsync(x=>x.Nombre == "Perro HTTP")).Id;
        var patologia = new Patologia { Descripcion = "Patología de prueba" }; db.Patologias.Add(patologia); await db.SaveChangesAsync(); patologiaHttp = patologia.Id;
    }
    await Exito("/Perros/Guardar", new() { ["Id"] = perroHttp.ToString(), ["Nombre"] = "Perro HTTP editado", ["FechaNacimiento"] = fecha, ["TutorId"] = tutorHttp.ToString(), ["GrupoEtarioId"] = grupoHttp.ToString(), ["TamanioId"] = tamanioHttp.ToString(), ["Reactivo"] = "true" });
    var pngPrueba = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6zQAAAABJRU5ErkJggg==");
    async Task<HttpResponseMessage> SubirFoto(int id, byte[] contenido, string nombreArchivo)
    {
        using var datos = new MultipartFormDataContent();
        datos.Add(new StringContent(tokenCatalogo), "__RequestVerificationToken");
        datos.Add(new StringContent(id.ToString()), "Id");
        datos.Add(new StringContent("Perro con foto"), "Nombre");
        datos.Add(new StringContent(fecha), "FechaNacimiento");
        datos.Add(new StringContent(tutorHttp.ToString()), "TutorId");
        datos.Add(new StringContent(grupoHttp.ToString()), "GrupoEtarioId");
        datos.Add(new StringContent(tamanioHttp.ToString()), "TamanioId");
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        datos.Add(archivo, "Foto", nombreArchivo);
        return await vet.PostAsync("/Perros/Guardar", datos);
    }
    using(var altaFoto = await SubirFoto(0, pngPrueba, "mascota.png"))
        Verificar(altaFoto.IsSuccessStatusCode && (await altaFoto.Content.ReadAsStringAsync()).Contains("true"), "No se pudo crear un perro con foto.");
    int perroFoto;
    string archivoInicial;
    await using(var scope = provider.CreateAsyncScope())
    {
        var perro = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Perros.SingleAsync(x=>x.Nombre == "Perro con foto");
        perroFoto = perro.Id; archivoInicial = perro.FotoArchivo!;
        Verificar(Regex.IsMatch(archivoInicial, "^[a-f0-9]{32}\\.png$"), "Se conservó el nombre original del archivo.");
    }
    using var propietarioFoto = Cliente(); await Ingresar(propietarioFoto, "http@prueba.invalid");
    using(var fotoVisible = await propietarioFoto.GetAsync("/Perros/Foto?id=" + perroFoto))
        Verificar(fotoVisible.IsSuccessStatusCode && fotoVisible.Content.Headers.ContentType!.MediaType == "image/png"
            && (await fotoVisible.Content.ReadAsByteArrayAsync()).SequenceEqual(pngPrueba), "El tutor propietario no ve la foto.");
    using(var fotoAjena = await tutorCliente.GetAsync("/Perros/Foto?id=" + perroFoto))
        Verificar(fotoAjena.StatusCode == HttpStatusCode.Redirect && fotoAjena.Headers.Location!.ToString().Contains("AccesoDenegado"), "Otro tutor pudo ver la foto.");
    using(var fotoAnonima = await anon.GetAsync("/Perros/Foto?id=" + perroFoto))
        Verificar(fotoAnonima.StatusCode == HttpStatusCode.Redirect && fotoAnonima.Headers.Location!.ToString().Contains("Ingresar"), "Una persona sin sesión pudo ver la foto.");
    using(var fotoPublica = await anon.GetAsync("/App_Data/Perros/" + archivoInicial))
        Verificar(fotoPublica.StatusCode == HttpStatusCode.NotFound, "La foto está publicada como archivo estático.");
    using(var reemplazo = await SubirFoto(perroFoto, pngPrueba, "../../reemplazo.png"))
        Verificar(reemplazo.IsSuccessStatusCode && (await reemplazo.Content.ReadAsStringAsync()).Contains("true"), "No se pudo reemplazar la foto.");
    string archivoReemplazado;
    await using(var scope = provider.CreateAsyncScope())
        archivoReemplazado = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Perros.SingleAsync(x=>x.Id == perroFoto)).FotoArchivo!;
    Verificar(archivoReemplazado != archivoInicial && File.Exists(Path.Combine(directorioFotosPrueba, archivoReemplazado))
        && !File.Exists(Path.Combine(directorioFotosPrueba, archivoInicial)), "El reemplazo no guardó la nueva foto o dejó la anterior.");
    await Exito("/Perros/Guardar", new() { ["Id"] = perroFoto.ToString(), ["Nombre"] = "Perro con foto", ["FechaNacimiento"] = fecha,
        ["TutorId"] = tutorHttp.ToString(), ["GrupoEtarioId"] = grupoHttp.ToString(), ["TamanioId"] = tamanioHttp.ToString() });
    foreach(var fotoInvalida in new[]{ (Datos: System.Text.Encoding.UTF8.GetBytes("<html>No es una imagen</html>"), Nombre: "falsa.png"),
        (Datos: pngPrueba, Nombre: "imagen.svg"), (Datos: new byte[ImagenPerroService.MaximoBytes + 1], Nombre: "grande.png") })
    {
        using var response = await SubirFoto(perroFoto, fotoInvalida.Datos, fotoInvalida.Nombre);
        Verificar(response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Contains("field-validation-error"), "Se aceptó una imagen inválida o demasiado grande.");
    }
    await using(var scope = provider.CreateAsyncScope())
        Verificar((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Perros.SingleAsync(x=>x.Id == perroFoto)).FotoArchivo == archivoReemplazado,
            "Editar sin imagen o rechazar un archivo perdió la foto guardada.");
    Verificar(Directory.GetFiles(directorioFotosPrueba).Length == 1, "Quedaron archivos de cargas inválidas.");
    var tarjetas = await (await propietarioFoto.GetAsync("/Perros/MisPerros")).Content.ReadAsStringAsync();
    Verificar(tarjetas.Contains("perro-card") && tarjetas.Contains("perro-card-datos") && tarjetas.Contains("Vacunas completas") && tarjetas.Contains("Historial"),
        "La tarjeta no muestra sus datos o el historial.");
    Console.WriteLine("Fotos y tarjetas: carga, reemplazo, conservación, formatos y tamaño, acceso privado y datos de la card verificados.");
    await Exito("/PerrosPatologias/Guardar", new() { ["PerroId"] = perroHttp.ToString(), ["PatologiaId"] = patologiaHttp.ToString(), ["Observacion"] = "Observación de prueba" });
    await Exito("/PerrosPatologias/Guardar", new() { ["EsEdicion"] = "true", ["PerroId"] = perroHttp.ToString(), ["PatologiaId"] = patologiaHttp.ToString(), ["Controlado"] = "true" });
    await Exito($"/PerrosPatologias/Eliminar?perroId={perroHttp}&patologiaId={patologiaHttp}", new());
    var menuVeterinario = await (await vet.GetAsync("/")).Content.ReadAsStringAsync();
    Verificar(menuVeterinario.Contains("/Servicios/Listado"), "Falta Servicios en el menú.");
    foreach(var catalogoOculto in new[]{"GruposEtarios","Tamanios","Patologias","Tratamientos","Estados"})
        Verificar(!menuVeterinario.Contains("/" + catalogoOculto + "/Listado"), "El menú ofrece un catálogo oculto: " + catalogoOculto);
    var formularioPerro = await (await vet.GetAsync("/Perros/ObtenerFormulario")).Content.ReadAsStringAsync();
    Verificar(!formularioPerro.Contains("value=\"-1\"") && !formularioPerro.Contains("NuevoGrupoEtario") && !formularioPerro.Contains("NuevoTamanio"), "Grupo etario o tamaño conservan la opción Otro.");
    var perroConOtros = new Dictionary<string,string> { ["Nombre"] = "No guardar", ["FechaNacimiento"] = fecha,
        ["TutorId"] = tutorHttp.ToString(), ["GrupoEtarioId"] = "-1", ["NuevoGrupoEtario"] = "No crear grupo",
        ["TamanioId"] = "-1", ["NuevoTamanio"] = "No crear tamaño" };
    using(var otroRechazado = await Enviar("/Perros/Guardar", perroConOtros))
        Verificar(otroRechazado.IsSuccessStatusCode && (await otroRechazado.Content.ReadAsStringAsync()).Contains("field-validation-error"), "Se aceptó Otro por envío manual.");
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Verificar(!await db.GruposEtarios.AnyAsync(x=>x.Descripcion == "No crear grupo")
            && !await db.Tamanios.AnyAsync(x=>x.Descripcion == "No crear tamaño")
            && !await db.Perros.AnyAsync(x=>x.Nombre == "No guardar"), "El envío inválido creó datos.");
        Verificar(await db.Estados.CountAsync() == 6, "Se modificaron los estados del circuito.");
    }
    await Exito("/PerrosPatologias/Guardar", new() { ["PerroId"] = perroHttp.ToString(), ["PatologiaId"] = "-1", ["NuevaPatologia"] = "Patología desde perro" });
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nueva = await db.Patologias.SingleAsync(x=>x.Descripcion == "Patología desde perro");
        Verificar(await db.PerrosPatologias.AnyAsync(x=>x.PerroId == perroHttp && x.PatologiaId == nueva.Id), "No se asignó la nueva patología.");
    }
    var formularioFijo = await (await vet.GetAsync("/PerrosPatologias/ObtenerFormulario?perroId=" + perroHttp)).Content.ReadAsStringAsync();
    Verificar(formularioFijo.Contains("Perro HTTP editado") && !Regex.IsMatch(formularioFijo, "<select[^>]*name=\"PerroId\""), "Se puede elegir otro perro desde el formulario contextual.");
    var contextoFijo = WebUtility.HtmlDecode(Regex.Match(formularioFijo, "name=\"ContextoPerro\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    using(var cambiarPerro = await Enviar("/PerrosPatologias/Guardar", new() { ["PerroId"] = otroPerroId.ToString(), ["PatologiaId"] = patologiaHttp.ToString(), ["ContextoPerro"] = contextoFijo }))
        Verificar(cambiarPerro.StatusCode == HttpStatusCode.Redirect && cambiarPerro.Headers.Location!.ToString().Contains("AccesoDenegado"), "El servidor aceptó cambiar el perro del contexto.");
    using(var invalidoFijo = await Enviar("/PerrosPatologias/Guardar", new() { ["PerroId"] = perroHttp.ToString(), ["PatologiaId"] = "-1", ["NuevaPatologia"] = "", ["ContextoPerro"] = contextoFijo }))
    {
        var html = await invalidoFijo.Content.ReadAsStringAsync();
        Verificar(invalidoFijo.IsSuccessStatusCode && html.Contains("Perro HTTP editado") && !Regex.IsMatch(html, "<select[^>]*name=\"PerroId\""), "Un error de validación perdió el perro fijo.");
    }
    await Exito("/Perros/Eliminar?id=" + perroHttp, new());
    await Exito("/Tutores/Eliminar?id=" + tutorHttp, new());
    var fechaCupo = Reloj.Hoy.AddDays(2).ToString("yyyy-MM-dd");
    await Exito("/CuposDiarios/Guardar", new() { ["FechaCupo"] = fechaCupo, ["NumeroCuposDisponibles"] = "2" });
    int cupoHttp;
    await using(var scope = provider.CreateAsyncScope()) cupoHttp = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().CuposDiarios.SingleAsync(x=>x.FechaCupo == Reloj.Hoy.AddDays(2))).Id;
    await Exito("/CuposDiarios/Guardar", new() { ["Id"] = cupoHttp.ToString(), ["FechaCupo"] = fechaCupo, ["NumeroCuposDisponibles"] = "3" });
    await Exito("/CuposDiarios/Eliminar?id=" + cupoHttp, new());
    Turno activo;
    await using(var scope = provider.CreateAsyncScope()) activo = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Turnos.AsNoTracking().SingleAsync(x=>x.EstadoId == EstadosTurno.Solicitado);
    await Exito("/Turnos/Guardar", new() { ["Id"] = activo.Id.ToString(), ["PerroId"] = activo.PerroId.ToString(), ["VeterinarioId"] = (activo.VeterinarioId?.ToString() ?? string.Empty), ["CupoDiarioId"] = activo.CupoDiarioId.ToString(), ["Observaciones"] = "Observación visible en historial" });
    var editarTratamiento = new Dictionary<string,string> { ["Id"] = activo.Id.ToString(), ["PerroId"] = activo.PerroId.ToString(),
        ["VeterinarioId"] = (activo.VeterinarioId?.ToString() ?? string.Empty), ["CupoDiarioId"] = activo.CupoDiarioId.ToString(),
        ["TratamientoId"] = "-1", ["NuevoTratamiento"] = "Tratamiento desde turno", ["Observaciones"] = "Observación de prueba" };
    await Exito("/Turnos/Guardar", editarTratamiento);
    editarTratamiento["NuevoTratamiento"] = " TRATAMIENTO DESDE TURNO ";
    await Exito("/Turnos/Guardar", editarTratamiento);
    editarTratamiento["PerroId"] = (activo.PerroId == perroId ? otroPerroId : perroId).ToString();
    editarTratamiento["NuevoTratamiento"] = "No crear tratamiento con turno inválido";
    using(var turnoInvalido = await Enviar("/Turnos/Guardar", editarTratamiento))
        Verificar(turnoInvalido.StatusCode == HttpStatusCode.BadRequest, "Se permitió cambiar la mascota del turno.");
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tratamiento = await db.Tratamientos.SingleAsync(x=>x.Descripcion == "Tratamiento desde turno");
        Verificar((await db.Turnos.SingleAsync(x=>x.Id == activo.Id)).TratamientoId == tratamiento.Id, "No se asignó el tratamiento nuevo.");
        Verificar(await db.Tratamientos.CountAsync(x=>x.Descripcion.ToLower() == "tratamiento desde turno") == 1
            && !await db.Tratamientos.AnyAsync(x=>x.Descripcion.StartsWith("No crear")), "El tratamiento se duplicó o quedó huérfano.");
    }
    var tokenTutor = await Token(tutorCliente, "/Turnos/SolicitarPersonal");
    var tratamientoForjado = await tutorCliente.PostAsync("/Turnos/GuardarPersonal", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = tokenTutor, ["PerroId"] = perroId.ToString(), ["VeterinarioId"] = vetPerfil.ToString(), ["CupoDiarioId"] = cupoId.ToString(),
        ["ServicioIds"] = servicioId.ToString(), ["TratamientoId"] = "-1", ["NuevoTratamiento"] = "El tutor no puede crear" }));
    Verificar(tratamientoForjado.StatusCode == HttpStatusCode.Redirect && tratamientoForjado.Headers.Location!.ToString().Contains("AccesoDenegado"), "El tutor pudo crear un tratamiento.");
    Console.WriteLine("Catálogos contextuales: menú, Otro, creación y reutilización, asignación, validación, ausencia de huérfanos y estados fijos verificados.");
    using(var detalles = await vet.GetAsync("/ServiciosPorTurno/Listado?turnoId=" + activo.Id)) Verificar(detalles.IsSuccessStatusCode, "Falló listado de servicios por turno.");
    await Exito("/ServiciosPorTurno/Guardar", new() { ["EsEdicion"] = "true", ["TurnoId"] = activo.Id.ToString(), ["ServicioId"] = servicioId.ToString(), ["Completado"] = "true" });
    await Exito("/Turnos/Confirmar?id=" + activo.Id, new());
    await Exito("/Turnos/Completar?id=" + activo.Id, new());
    using(var modificarFinalizado = await Enviar("/ServiciosPorTurno/Guardar", new() { ["EsEdicion"] = "true", ["TurnoId"] = activo.Id.ToString(), ["ServicioId"] = servicioId.ToString(), ["Completado"] = "false" }))
        Verificar(modificarFinalizado.StatusCode == HttpStatusCode.BadRequest, "Se modificó un turno finalizado.");
    var perroAjeno = await tutorCliente.GetAsync("/Turnos/Historial?perroId=" + otroPerroId);
    Verificar(perroAjeno.StatusCode == HttpStatusCode.Redirect && perroAjeno.Headers.Location!.ToString().Contains("AccesoDenegado"), "Se consultó un historial ajeno.");
    using(var historico = await vet.GetAsync("/Turnos/Historial?perroId=" + activo.PerroId)) Verificar((await historico.Content.ReadAsStringAsync()).Contains("Observaci"), "No se renderizó el historial.");
    Console.WriteLine("CRUDs HTTP: altas, ediciones y bajas de cuentas, teléfonos, perros, asociaciones, catálogos y cupos; servicios e historial y transiciones de turnos verificados.");
    await using (var scope = provider.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<CuentaService>().EliminarAsync(dualPerfil, false, vetId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Verificar(!(await db.Users.SingleAsync(x => x.Id == dualId)).Activo, "La baja no desactivó la cuenta completa.");
    }
    var bloqueado = await dualCliente.GetAsync("/Veterinarios/Listado");
    Verificar(bloqueado.StatusCode == HttpStatusCode.Redirect && bloqueado.Headers.Location!.ToString().Contains("Ingresar"), "La sesión dual siguió activa tras la baja.");
    var tokenBaja = await Token(dualCliente, "/Identity/Cuenta/Ingresar");
    var reingreso = await dualCliente.PostAsync("/Identity/Cuenta/Ingresar", new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",tokenBaja},{"Input.Email","dual@prueba.invalid"},{"Input.Password",password}}));
    Verificar(reingreso.StatusCode == HttpStatusCode.OK, "Una cuenta dada de baja volvió a ingresar.");
    Console.WriteLine("HTTP: listados, formularios, alta AJAX, estados fijos, antifalsificación, permisos y bloqueo de cuenta multirrol verificados.");
    // La semilla se prueba dos veces antes de usarla sobre la base de desarrollo.
    int lunaDemo, miloDemo, turnoLuna, turnoMilo, vetDemo, cupoDemo, servicioDemo;
    await using(var scope = provider.CreateAsyncScope())
    {
        await SemillaDemo.CrearAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cuentasDemo = await db.Users.CountAsync(x => x.Email!.EndsWith("@vetweb.invalid"));
        var perrosDemo = await db.Perros.CountAsync(x => x.Nombre.EndsWith("(demo)"));
        var turnosDemo = await db.Turnos.CountAsync(x => x.Observaciones!.StartsWith("Semilla demo:"));
        await SemillaDemo.CrearAsync(scope.ServiceProvider);
        Verificar(cuentasDemo == 3 && perrosDemo == 2 && turnosDemo == 2, "La semilla no creó los datos esperados.");
        Verificar(await db.Users.CountAsync(x => x.Email!.EndsWith("@vetweb.invalid")) == cuentasDemo
            && await db.Perros.CountAsync(x => x.Nombre.EndsWith("(demo)")) == perrosDemo
            && await db.Turnos.CountAsync(x => x.Observaciones!.StartsWith("Semilla demo:")) == turnosDemo, "La semilla duplicó datos.");
        lunaDemo = (await db.Perros.SingleAsync(x => x.Nombre == "Luna (demo)")).Id;
        miloDemo = (await db.Perros.SingleAsync(x => x.Nombre == "Milo (demo)")).Id;
        var turnoL = await db.Turnos.SingleAsync(x => x.PerroId == lunaDemo); turnoLuna = turnoL.Id;
        var turnoM = await db.Turnos.SingleAsync(x => x.PerroId == miloDemo); turnoMilo = turnoM.Id;
        vetDemo = turnoM.VeterinarioId!.Value; cupoDemo = turnoM.CupoDiarioId;
        servicioDemo = (await db.Servicios.SingleAsync(x=>x.Descripcion == "Demo - Baño")).Id;
    }
    using var demoVet = Cliente(); using var demoTutor = Cliente(); using var demoDual = Cliente();
    await Ingresar(demoVet, SemillaDemo.EmailVeterinario, SemillaDemo.Password);
    await Ingresar(demoTutor, SemillaDemo.EmailTutor, SemillaDemo.Password);
    await Ingresar(demoDual, SemillaDemo.EmailDoble, SemillaDemo.Password);
    using var demoDualVet = Cliente();
    await Ingresar(demoDualVet, SemillaDemo.EmailDoble, SemillaDemo.Password);
    var sinElegir = await demoDual.GetAsync("/");
    Verificar(sinElegir.StatusCode == HttpStatusCode.Redirect && sinElegir.Headers.Location!.ToString().Contains("SeleccionarRol"), "No se exigió elegir un rol.");
    var sinTokenRol = await demoDual.PostAsync("/Cuenta/SeleccionarRol", new FormUrlEncodedContent(new Dictionary<string,string> { ["rol"] = "Veterinario" }));
    Verificar(sinTokenRol.StatusCode == HttpStatusCode.BadRequest, "Se eligió rol sin token antifalsificación.");
    async Task ElegirRol(HttpClient client, string rol)
    {
        var token = await Token(client, "/Cuenta/SeleccionarRol");
        var response = await client.PostAsync("/Cuenta/SeleccionarRol", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token, ["rol"] = rol }));
        Verificar(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString() == "/", "Falló elección del rol.");
    }
    await ElegirRol(demoDual, "Tutor");
    await ElegirRol(demoDualVet, "Veterinario");
    var tokenIntentoCambio = await Token(demoDual, "/Turnos/SolicitarPersonal");
    var cambiarDentroSesion = await demoDual.PostAsync("/Cuenta/SeleccionarRol", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenIntentoCambio, ["rol"] = "Veterinario" }));
    Verificar(cambiarDentroSesion.StatusCode == HttpStatusCode.Redirect && cambiarDentroSesion.Headers.Location!.ToString().Contains("AccesoDenegado"), "Se cambió el rol dentro de la misma sesión.");
    foreach(var ruta in new[]{"/Tutores/Listado", "/Perros/ObtenerFormulario", "/Servicios/Listado"})
    {
        var response = await demoDual.GetAsync(ruta);
        Verificar(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("AccesoDenegado"), "El rol Tutor alcanzó " + ruta);
    }
    var tutorDesdeVet = await demoDualVet.GetAsync("/Perros/MisPerros");
    Verificar(tutorDesdeVet.StatusCode == HttpStatusCode.Redirect && tutorDesdeVet.Headers.Location!.ToString().Contains("AccesoDenegado"), "El rol Veterinario alcanzó una ruta exclusiva de Tutor.");
    var inicioDualVet = await (await demoDualVet.GetAsync("/")).Content.ReadAsStringAsync();
    Verificar(inicioDualVet.Contains("Administración veterinaria") && !inicioDualVet.Contains("Mi espacio como tutor"), "El menú de Veterinario ofrece opciones del otro rol.");
    bool Denegado(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Forbidden
        || response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("AccesoDenegado");
    var agendaMarina = await (await demoVet.GetAsync("/Turnos/Listado")).Content.ReadAsStringAsync();
    var agendaAlex = await (await demoDualVet.GetAsync("/Turnos/Listado")).Content.ReadAsStringAsync();
    Verificar(agendaMarina.Contains("Luna (demo)") && agendaMarina.Contains("Milo (demo)"), "El veterinario asignado no ve sus turnos.");
    Verificar(!agendaAlex.Contains("Luna (demo)") && !agendaAlex.Contains("Milo (demo)"), "Otro veterinario ve turnos asignados a Marina.");
    foreach(var rutaAjena in new[]{"/Turnos/ObtenerFormulario?id=" + turnoLuna, "/ServiciosPorTurno/Listado?turnoId=" + turnoLuna,
        "/ServiciosPorTurno/ObtenerFormulario?turnoId=" + turnoLuna})
    {
        using var response = await demoDualVet.GetAsync(rutaAjena);
        Verificar(Denegado(response), "Se accedió a un turno de otro veterinario: " + rutaAjena);
    }
    var historialLimitado = await (await demoDualVet.GetAsync("/Turnos/Historial?perroId=" + lunaDemo)).Content.ReadAsStringAsync();
    Verificar(!historialLimitado.Contains("Semilla demo: Luna"), "El historial mostró un turno asignado a otro profesional.");
    var tokenAgendaAlex = await Token(demoDualVet, "/Turnos/ObtenerFormulario");
    foreach(var accionAjena in new[]{"Confirmar","Rechazar","Cancelar","Completar"})
    {
        using var response = await demoDualVet.PostAsync($"/Turnos/{accionAjena}?id={turnoLuna}", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenAgendaAlex }));
        Verificar(Denegado(response), "Se permitió operar un turno ajeno: " + accionAjena);
    }
    var servicioAjeno = await demoDualVet.PostAsync("/ServiciosPorTurno/Guardar", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = tokenAgendaAlex, ["TurnoId"] = turnoLuna.ToString(), ["ServicioId"] = servicioDemo.ToString(), ["EsEdicion"] = "true", ["Completado"] = "true" }));
    Verificar(Denegado(servicioAjeno), "Se modificó el servicio de otro veterinario.");
    var eliminarServicioAjeno = await demoDualVet.PostAsync($"/ServiciosPorTurno/Eliminar?turnoId={turnoLuna}&servicioId={servicioDemo}", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenAgendaAlex }));
    Verificar(Denegado(eliminarServicioAjeno), "Se eliminó el servicio de otro veterinario.");
    var editarAjeno = await demoDualVet.PostAsync("/Turnos/Guardar", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = tokenAgendaAlex, ["Id"] = turnoLuna.ToString(), ["PerroId"] = lunaDemo.ToString(),
        ["VeterinarioId"] = vetDemo.ToString(), ["CupoDiarioId"] = cupoDemo.ToString(), ["Observaciones"] = "No modificar" }));
    Verificar(Denegado(editarAjeno), "Se editó un turno de otro profesional.");
    int cupoCompartido;
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nuevoCupo = new CupoDiario { FechaCupo = Reloj.Hoy.AddDays(10), NumeroCuposDisponibles = 2 };
        db.CuposDiarios.Add(nuevoCupo); await db.SaveChangesAsync(); cupoCompartido = nuevoCupo.Id;
    }
    var tokenSolicitudCompartida = await Token(demoDual, "/Turnos/SolicitarPersonal");
    var solicitudSinVeterinario = await demoDual.PostAsync("/Turnos/GuardarPersonal", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = tokenSolicitudCompartida, ["PerroId"] = miloDemo.ToString(), ["VeterinarioId"] = "",
        ["CupoDiarioId"] = cupoCompartido.ToString(), ["ServicioIds"] = servicioDemo.ToString() }));
    Verificar(solicitudSinVeterinario.IsSuccessStatusCode && (await solicitudSinVeterinario.Content.ReadAsStringAsync()).Contains("true"), "No se pudo solicitar con No especifica.");
    int turnoCompartido;
    await using(var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var compartido = await db.Turnos.SingleAsync(x=>x.CupoDiarioId == cupoCompartido);
        Verificar(compartido.VeterinarioId is null, "No especifica no se guardó como opcional."); turnoCompartido = compartido.Id;
    }
    foreach(var profesionalCliente in new[]{demoVet,demoDualVet})
    {
        var agenda = await (await profesionalCliente.GetAsync("/Turnos/Listado")).Content.ReadAsStringAsync();
        Verificar(agenda.Contains("No especifica"), "Un veterinario no ve los turnos compartidos.");
        using var editarCompartido = await profesionalCliente.GetAsync("/Turnos/ObtenerFormulario?id=" + turnoCompartido);
        using var verServiciosCompartidos = await profesionalCliente.GetAsync("/ServiciosPorTurno/Listado?turnoId=" + turnoCompartido);
        Verificar(editarCompartido.IsSuccessStatusCode && verServiciosCompartidos.IsSuccessStatusCode, "Se ocultó un turno sin profesional especificado.");
    }
    var confirmarCompartido = await demoDualVet.PostAsync("/Turnos/Confirmar?id=" + turnoCompartido, new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenAgendaAlex }));
    Verificar(confirmarCompartido.IsSuccessStatusCode, "Un veterinario no pudo atender un turno compartido.");
    Console.WriteLine("Agenda profesional: turnos asignados privados, URLs y operaciones ajenas bloqueadas, No especifica visible y operativo para todos.");
    var inicioVet = await (await demoVet.GetAsync("/")).Content.ReadAsStringAsync();
    var inicioTutor = await (await demoTutor.GetAsync("/")).Content.ReadAsStringAsync();
    var inicioDual = await (await demoDual.GetAsync("/")).Content.ReadAsStringAsync();
    Verificar(inicioVet.Contains("Administración veterinaria") && !inicioVet.Contains("Mi espacio como tutor"), "Inicio de veterinario incorrecto.");
    Verificar(inicioTutor.Contains("Mi espacio como tutor") && !inicioTutor.Contains("Administración veterinaria"), "Inicio de tutor incorrecto.");
    Verificar(!inicioDual.Contains("Administración veterinaria") && inicioDual.Contains("Mi espacio como tutor"), "El menú de Tutor ofrece opciones del otro rol.");
    var misPerros = await (await demoDual.GetAsync("/Perros/MisPerros")).Content.ReadAsStringAsync();
    Verificar(misPerros.Contains("Milo (demo)") && !misPerros.Contains("Luna (demo)") && !misPerros.Contains("data-form-url="), "La vista personal expone mascotas o acciones administrativas.");
    var cuposPersonales = await (await demoDual.GetAsync("/CuposDiarios/Disponibles")).Content.ReadAsStringAsync();
    Verificar(cuposPersonales.Contains("Cupos disponibles") && !cuposPersonales.Contains("data-form-url="), "Los cupos personales exponen acciones administrativas.");
    var todosPerros = await (await demoDualVet.GetAsync("/Perros/Listado")).Content.ReadAsStringAsync();
    Verificar(todosPerros.Contains("Milo (demo)") && todosPerros.Contains("Luna (demo)"), "La vista veterinaria no reúne todos los perros.");
    var misTurnos = await (await demoDual.GetAsync("/Turnos/MisTurnos")).Content.ReadAsStringAsync();
    Verificar(misTurnos.Contains("Milo (demo)") && !misTurnos.Contains("Luna (demo)") && !misTurnos.Contains("data-delete-url=\"/Turnos/Confirmar"), "La vista personal expone turnos o acciones ajenas.");
    var historialAjeno = await demoDual.GetAsync($"/Turnos/Historial?perroId={lunaDemo}&personal=true");
    Verificar(historialAjeno.StatusCode == HttpStatusCode.Redirect, "El contexto tutor multirrol permitió un historial ajeno.");
    var serviciosAjenos = await demoDual.GetAsync($"/ServiciosPorTurno/Listado?turnoId={turnoLuna}&personal=true");
    Verificar(serviciosAjenos.StatusCode == HttpStatusCode.Redirect, "El contexto tutor multirrol permitió servicios ajenos.");
    var tokenDual = await Token(demoDual, "/Turnos/SolicitarPersonal");
    var forjado = await demoDual.PostAsync("/Turnos/GuardarPersonal", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = tokenDual, ["PerroId"] = lunaDemo.ToString(), ["VeterinarioId"] = vetDemo.ToString(),
        ["CupoDiarioId"] = cupoDemo.ToString(), ["ServicioIds"] = servicioDemo.ToString() }));
    Verificar(forjado.StatusCode == HttpStatusCode.BadRequest, "El endpoint personal aceptó una mascota ajena.");
    var cancelacionAjena = await demoDual.PostAsync("/Turnos/CancelarPersonal?id=" + turnoLuna, new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenDual }));
    Verificar(cancelacionAjena.StatusCode == HttpStatusCode.BadRequest, "El endpoint personal canceló un turno ajeno.");
    var cancelacionPropia = await demoDual.PostAsync("/Turnos/CancelarPersonal?id=" + turnoMilo, new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenDual }));
    Verificar(cancelacionPropia.IsSuccessStatusCode, "Falló la cancelación personal.");
    await using(var scope = provider.CreateAsyncScope())
        Verificar((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Turnos.SingleAsync(x=>x.Id == turnoMilo)).EstadoId == EstadosTurno.CanceladoTutor, "La cancelación personal se atribuyó al veterinario.");
    Verificar(inicioDual.Contains("/Cuenta/CambiarRol") && !inicioTutor.Contains("/Cuenta/CambiarRol") && !inicioVet.Contains("/Cuenta/CambiarRol"), "El desplegable no distingue las cuentas multirrol.");
    var cambioSinToken = await demoDual.PostAsync("/Cuenta/CambiarRol", new FormUrlEncodedContent(new Dictionary<string,string>()));
    Verificar(cambioSinToken.StatusCode == HttpStatusCode.BadRequest, "Se cerró la sesión sin token.");
    var tokenUnico = await Token(demoTutor, "/Turnos/SolicitarPersonal");
    var cambioUnico = await demoTutor.PostAsync("/Cuenta/CambiarRol", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenUnico }));
    Verificar(cambioUnico.StatusCode == HttpStatusCode.Redirect && cambioUnico.Headers.Location!.ToString().Contains("AccesoDenegado"), "Una cuenta de un solo rol pudo usar Cambiar rol.");
    var tokenSalida = await Token(demoDual, "/Identity/Cuenta/Salir");
    var salida = await demoDual.PostAsync("/Cuenta/CambiarRol", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = tokenSalida }));
    Verificar(salida.StatusCode == HttpStatusCode.Redirect && salida.Headers.Location!.ToString().Contains("SeleccionarRol"), "Cambiar rol no volvió al selector.");
    var sesionCerrada = await demoDual.GetAsync("/Turnos/MisTurnos");
    Verificar(sesionCerrada.StatusCode == HttpStatusCode.Redirect && sesionCerrada.Headers.Location!.ToString().Contains("SeleccionarRol"), "Se conservaron permisos mientras no se había elegido el nuevo rol.");
    var selectorSinReingreso = await demoDual.GetAsync("/Cuenta/SeleccionarRol");
    Verificar(selectorSinReingreso.IsSuccessStatusCode && (await selectorSinReingreso.Content.ReadAsStringAsync()).Contains("Cerrar sesión"), "El cambio de rol perdió la autenticación.");
    await ElegirRol(demoDual, "Veterinario");
    var nuevoIngreso = await (await demoDual.GetAsync("/")).Content.ReadAsStringAsync();
    Verificar(nuevoIngreso.Contains("Administración veterinaria") && !nuevoIngreso.Contains("Mi espacio como tutor"), "El rol anterior persistió después del cambio.");
    Console.WriteLine("Acceso por sesión: elección obligatoria, un solo rol activo, cambio sin reingreso y bloqueo de URLs del otro rol, pertenencia y semilla repetible verificados.");

}
finally
{
    if (servidor is not null && !servidor.HasExited) { servidor.Kill(entireProcessTree: true); await servidor.WaitForExitAsync(); }
    NpgsqlConnection.ClearAllPools();
    if (!Regex.IsMatch(temporal, "^vetweb_prueba_[a-f0-9]{32}$") || temporal == original.Database) throw new Exception("Nombre de base temporal inválido.");
    await using var eliminar = new NpgsqlCommand($"DROP DATABASE \"{temporal}\" WITH (FORCE)", conexionAdmin);
    await eliminar.ExecuteNonQueryAsync();
    var rutaFotos = Path.GetFullPath(directorioFotosPrueba);
    var raizTemporal = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!rutaFotos.StartsWith(raizTemporal, StringComparison.OrdinalIgnoreCase)
        || !Regex.IsMatch(Path.GetFileName(rutaFotos), "^vetweb_prueba_[a-f0-9]{32}_fotos$"))
        throw new Exception("La ruta de fotos de prueba no está dentro de la carpeta temporal esperada.");
    if (Directory.Exists(rutaFotos)) Directory.Delete(rutaFotos, recursive: true);
    Console.WriteLine("Base temporal eliminada. La base de trabajo no se modificó.");
}
