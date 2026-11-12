using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Services;

internal static class ApiCoberturaPruebas
{
    private static void Exigir(bool valor, string mensaje) { if (!valor) throw new Exception("Cobertura API: " + mensaje); }
    public static async Task EjecutarAsync(ServiceProvider provider, HttpClient vet, HttpClient otroVet, HttpClient tutor, HttpClient otroTutor,
        HttpClient admin, HttpClient anon, HttpClient adminEnRolVet, int perroDemo, string password, string tokenVet, string tokenTutor, string tokenAdmin, string tokenOtroVet)
    {
        async Task<HttpResponseMessage> Enviar(HttpClient cliente, HttpMethod metodo, string ruta, object? datos, string? token)
        {
            using var request = new HttpRequestMessage(metodo, ruta);
            if (datos is not null) request.Content = JsonContent.Create(datos);
            if (token is not null) request.Headers.Add("RequestVerificationToken", token);
            return await cliente.SendAsync(request);
        }
        async Task Codigo(HttpClient cliente, HttpMethod metodo, string ruta, object? datos, string? token, HttpStatusCode esperado)
        {
            using var respuesta = await Enviar(cliente, metodo, ruta, datos, token);
            Exigir(respuesta.StatusCode == esperado, $"{metodo} {ruta}: esperado {esperado}, recibido {respuesta.StatusCode}.");
        }
        async Task<T> Crear<T>(HttpClient cliente, string ruta, object datos, string token)
        {
            using var respuesta = await Enviar(cliente, HttpMethod.Post, ruta, datos, token);
            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await respuesta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                var detalle = error.TryGetProperty("mensaje", out var mensaje) ? mensaje.GetString()
                    : error.TryGetProperty("errors", out var errores) ? errores.ToString() : "Validación rechazada";
                throw new Exception($"Cobertura API: no se creó {ruta}: {detalle}");
            }
            Exigir(respuesta.StatusCode == HttpStatusCode.Created, $"No se creó {ruta}: {respuesta.StatusCode}.");
            Exigir(respuesta.Headers.Location is not null, "Falta Location.");
            using var seguimiento = await cliente.GetAsync(respuesta.Headers.Location);
            Exigir(seguimiento.IsSuccessStatusCode, "Location no apunta a un recurso consultable.");
            return (await respuesta.Content.ReadFromJsonAsync<T>())!;
        }
        foreach (var ruta in new[] { "/api/perros", "/api/tutores", "/api/veterinarios", "/api/servicios", "/api/grupos-etarios", "/api/tamanios", "/api/patologias", "/api/tratamientos", "/api/estados", "/api/cupos-diarios" })
            await Codigo(anon, HttpMethod.Get, ruta, null, null, HttpStatusCode.Unauthorized);
        foreach (var catalogo in new[] { "servicios", "grupos-etarios", "tamanios", "patologias", "tratamientos" })
        {
            var ruta = "/api/" + catalogo;
            var dato = await Crear<CatalogoDto>(vet, ruta, new { descripcion = " API " + catalogo + " " }, tokenVet);
            Exigir(dato.Descripcion == "API " + catalogo, "No se normalizó el catálogo.");
            await Codigo(vet, HttpMethod.Put, $"{ruta}/{dato.Id}", new { descripcion = "API editado " + catalogo }, tokenVet, HttpStatusCode.NoContent);
            await Codigo(vet, HttpMethod.Post, ruta, new { descripcion = " api EDITADO " + catalogo + " " }, tokenVet, HttpStatusCode.BadRequest);
            await Codigo(vet, HttpMethod.Post, ruta, new { descripcion = "Sin antifalsificación" }, null, HttpStatusCode.BadRequest);
            await Codigo(tutor, HttpMethod.Post, ruta, new { descripcion = "No permitido" }, tokenTutor, HttpStatusCode.Forbidden);
            var administrativo = await Crear<CatalogoDto>(admin, ruta, new { descripcion = "API admin " + catalogo }, tokenAdmin);
            await Codigo(admin, HttpMethod.Put, $"{ruta}/{administrativo.Id}", new { descripcion = "API admin editado " + catalogo }, tokenAdmin, HttpStatusCode.NoContent);
            await Codigo(admin, HttpMethod.Delete, $"{ruta}/{administrativo.Id}", null, tokenAdmin, HttpStatusCode.NoContent);
            await Codigo(tutor, HttpMethod.Get, ruta, null, null, catalogo is "patologias" or "tratamientos" ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            await Codigo(vet, HttpMethod.Get, ruta + "?pagina=0", null, null, HttpStatusCode.BadRequest);
            await Codigo(vet, HttpMethod.Delete, $"{ruta}/{dato.Id}", null, tokenVet, HttpStatusCode.NoContent);
            await Codigo(vet, HttpMethod.Get, $"{ruta}/{dato.Id}", null, null, HttpStatusCode.NotFound);
        }
        var estados = (await tutor.GetFromJsonAsync<List<CatalogoDto>>("/api/estados"))!;
        Exigir(estados.Select(x => x.Id).SequenceEqual(Enumerable.Range(1, 6)), "Los estados no conservan los IDs de VetWeb.");
        await Codigo(vet, HttpMethod.Put, "/api/estados/1", new { descripcion = "No alterar" }, tokenVet, HttpStatusCode.MethodNotAllowed);
        await Codigo(vet, HttpMethod.Get, "/api/estados/99999", null, null, HttpStatusCode.NotFound);
        var cuenta = new CuentaDto { Nombre = "Alta", Apellido = "API", Email = "alta-api@prueba.invalid", Password = password };
        var nuevoTutor = await Crear<PerfilRespuestaDto>(vet, "/api/tutores", cuenta, tokenVet);
        await Codigo(tutor, HttpMethod.Get, "/api/tutores", null, null, HttpStatusCode.Forbidden);
        await Codigo(admin, HttpMethod.Get, "/api/tutores", null, null, HttpStatusCode.Forbidden);
        await Codigo(vet, HttpMethod.Get, "/api/veterinarios", null, null, HttpStatusCode.Forbidden);
        await Codigo(adminEnRolVet, HttpMethod.Get, "/api/veterinarios", null, null, HttpStatusCode.Forbidden);
        cuenta.Nombre = "Editado";
        await Codigo(vet, HttpMethod.Put, $"/api/tutores/{nuevoTutor.Id}", cuenta, tokenVet, HttpStatusCode.NoContent);
        using (var segura = await vet.GetAsync($"/api/tutores/{nuevoTutor.Id}"))
        {
            var json = await segura.Content.ReadAsStringAsync();
            Exigir(!json.Contains("password", StringComparison.OrdinalIgnoreCase) && !json.Contains("securityStamp", StringComparison.OrdinalIgnoreCase), "Se expusieron datos internos de Identity.");
        }
        await Codigo(vet, HttpMethod.Post, "/api/tutores", new CuentaDto { Nombre = "No", Apellido = "Vincular", Email = "admin@prueba.invalid", VincularExistente = true }, tokenVet, HttpStatusCode.BadRequest);
        var cuentaVet = new CuentaDto { Nombre = "Vet", Apellido = "API", Email = "vet-api@prueba.invalid", Password = password, Matricula = 91001 };
        var nuevoVet = await Crear<PerfilRespuestaDto>(admin, "/api/veterinarios", cuentaVet, tokenAdmin);
        cuentaVet.Matricula = 91002;
        await Codigo(admin, HttpMethod.Put, $"/api/veterinarios/{nuevoVet.Id}", cuentaVet, tokenAdmin, HttpStatusCode.NoContent);
        await Codigo(admin, HttpMethod.Delete, $"/api/veterinarios/{nuevoVet.Id}", null, tokenAdmin, HttpStatusCode.NoContent);
        await Codigo(admin, HttpMethod.Get, $"/api/veterinarios/{nuevoVet.Id}", null, null, HttpStatusCode.NotFound);
        var telefono = await Crear<TelefonoTutorDto>(vet, $"/api/tutores/{nuevoTutor.Id}/telefonos", new { nombrePropietario = "Contacto", numero = "+54 11 12345678" }, tokenVet);
        await Codigo(vet, HttpMethod.Put, $"/api/tutores/{nuevoTutor.Id}/telefonos/{telefono.Id}", new { nombrePropietario = "Contacto editado", numero = "+54 11 23456789" }, tokenVet, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Delete, $"/api/tutores/{int.MaxValue}/telefonos/{telefono.Id}", null, tokenVet, HttpStatusCode.NotFound);
        await Codigo(tutor, HttpMethod.Get, $"/api/tutores/{nuevoTutor.Id}/telefonos", null, null, HttpStatusCode.Forbidden);
        await Codigo(vet, HttpMethod.Post, $"/api/tutores/{nuevoTutor.Id}/telefonos", new { nombrePropietario = "X", numero = "inválido" }, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Delete, $"/api/tutores/{nuevoTutor.Id}/telefonos/{telefono.Id}", null, tokenVet, HttpStatusCode.NoContent);
        int tutorId, grupo, tamanio, profesional;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var perro = await db.Perros.SingleAsync(x => x.Id == perroDemo); tutorId = perro.TutorId; grupo = perro.GrupoEtarioId; tamanio = perro.TamanioId;
            profesional = await db.Veterinarios.Where(x => x.Usuario.Email == SemillaDemo.EmailVeterinario).Select(x => x.Id).SingleAsync();
        }
        var perroDto = new PerroDto { Nombre = "Perro API", FechaNacimiento = Reloj.Hoy.AddYears(-3), TutorId = tutorId, GrupoEtarioId = grupo, TamanioId = tamanio };
        var perroNuevo = await Crear<PerroRespuestaDto>(vet, "/api/perros", perroDto, tokenVet);
        await Codigo(tutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}", null, null, HttpStatusCode.OK);
        await Codigo(otroTutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}", null, null, HttpStatusCode.NotFound);
        await Codigo(otroTutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}/historial", null, null, HttpStatusCode.NotFound);
        Exigir(!(await otroTutor.GetFromJsonAsync<List<PerroRespuestaDto>>($"/api/perros?tutorId={tutorId}"))!.Any(), "El filtro tutorId amplió la visibilidad.");
        perroDto.Nombre = "Perro API editado";
        await Codigo(vet, HttpMethod.Put, $"/api/perros/{perroNuevo.Id}", perroDto, tokenVet, HttpStatusCode.NoContent);
        perroDto.FechaNacimiento = Reloj.Hoy.AddDays(1);
        await Codigo(vet, HttpMethod.Put, $"/api/perros/{perroNuevo.Id}", perroDto, tokenVet, HttpStatusCode.BadRequest);
        perroDto.FechaNacimiento = Reloj.Hoy.AddYears(-3);
        await Codigo(tutor, HttpMethod.Put, $"/api/perros/{perroNuevo.Id}", perroDto, tokenTutor, HttpStatusCode.Forbidden);
        var patologia = await Crear<PatologiaPerroRespuestaDto>(vet, $"/api/perros/{perroNuevo.Id}/patologias", new { patologiaId = -1, nuevaPatologia = " Patología API ", observacion = "Observación" }, tokenVet);
        await Codigo(vet, HttpMethod.Post, $"/api/perros/{perroNuevo.Id}/patologias", new { patologiaId = -1, nuevaPatologia = " PATOLOGÍA API " }, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Put, $"/api/perros/{perroNuevo.Id}/patologias/{patologia.PatologiaId}", new { controlado = true, observacion = "Controlado" }, tokenVet, HttpStatusCode.NoContent);
        await Codigo(tutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}/patologias", null, null, HttpStatusCode.OK);
        await Codigo(otroTutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}/patologias", null, null, HttpStatusCode.NotFound);
        await Codigo(vet, HttpMethod.Delete, $"/api/perros/{perroDemo}/patologias/{patologia.PatologiaId}", null, tokenVet, HttpStatusCode.NotFound);
        await Codigo(vet, HttpMethod.Delete, $"/api/perros/{perroNuevo.Id}/patologias/{patologia.PatologiaId}", null, tokenVet, HttpStatusCode.NoContent);
        var cupo = await Crear<CupoDiarioDto>(vet, "/api/cupos-diarios", new CupoDiarioDto { FechaCupo = Reloj.Hoy.AddDays(50), NumeroCuposDisponibles = 3 }, tokenVet);
        await Codigo(tutor, HttpMethod.Post, "/api/cupos-diarios", new CupoDiarioDto { FechaCupo = Reloj.Hoy, NumeroCuposDisponibles = 1 }, tokenTutor, HttpStatusCode.Forbidden);
        await Codigo(vet, HttpMethod.Post, "/api/cupos-diarios", new CupoDiarioDto { FechaCupo = Reloj.Hoy.AddDays(-1), NumeroCuposDisponibles = 1 }, tokenVet, HttpStatusCode.BadRequest);
        var s1 = (await vet.GetFromJsonAsync<List<CatalogoDto>>("/api/servicios"))!.First().Id;
        var s2 = await Crear<CatalogoDto>(vet, "/api/servicios", new { descripcion = "Servicio API por turno" }, tokenVet);
        var solicitud = new TurnoDto { PerroId = perroNuevo.Id, CupoDiarioId = cupo.Id, VeterinarioId = profesional, ServicioIds = [s1] };
        var turno = await Crear<TurnoRespuestaDto>(tutor, "/api/turnos", solicitud, tokenTutor);
        await Codigo(vet, HttpMethod.Delete, $"/api/perros/{perroNuevo.Id}", null, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Delete, $"/api/cupos/{cupo.Id}", null, tokenVet, HttpStatusCode.BadRequest);
        cupo.FechaCupo = Reloj.Hoy.AddDays(51);
        await Codigo(vet, HttpMethod.Put, $"/api/cupos-diarios/{cupo.Id}", cupo, tokenVet, HttpStatusCode.BadRequest);
        perroDto.TutorId = nuevoTutor.Id;
        await Codigo(vet, HttpMethod.Put, $"/api/perros/{perroNuevo.Id}", perroDto, tokenVet, HttpStatusCode.BadRequest);
        perroDto.TutorId = tutorId;
        solicitud.Observaciones = "Editado por API";
        await Codigo(vet, HttpMethod.Put, $"/api/turnos/{turno.Id}", solicitud, tokenVet, HttpStatusCode.NoContent);
        await Codigo(tutor, HttpMethod.Put, $"/api/turnos/{turno.Id}", solicitud, tokenTutor, HttpStatusCode.Forbidden);
        await Codigo(otroVet, HttpMethod.Get, $"/api/turnos/{turno.Id}/servicios", null, null, HttpStatusCode.NotFound);
        await Codigo(otroVet, HttpMethod.Put, $"/api/turnos/{turno.Id}/servicios/{s1}", new { completado = true }, tokenOtroVet, HttpStatusCode.Forbidden);
        await Codigo(otroVet, HttpMethod.Put, $"/api/turnos/{turno.Id}", solicitud, tokenOtroVet, HttpStatusCode.NotFound);
        var historialOtro = (await otroVet.GetFromJsonAsync<List<TurnoRespuestaDto>>($"/api/perros/{perroNuevo.Id}/historial"))!;
        Exigir(historialOtro.All(x => x.Id != turno.Id), "El historial expuso un turno de otro profesional.");
        await Codigo(vet, HttpMethod.Post, $"/api/turnos/{turno.Id}/servicios", new { servicioId = s2.Id }, tokenVet, HttpStatusCode.Created);
        await Codigo(vet, HttpMethod.Delete, $"/api/servicios/{s2.Id}", null, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Put, $"/api/turnos/{turno.Id}/servicios/{s2.Id}", new { completado = true }, tokenVet, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Delete, $"/api/turnos/{turno.Id}/servicios/{s2.Id}", null, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Put, $"/api/turnos/{turno.Id}/servicios/{s2.Id}", new { completado = false }, tokenVet, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Delete, $"/api/turnos/{turno.Id}/servicios/{s2.Id}", null, tokenVet, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Delete, $"/api/turnos/{turno.Id}/servicios/{s1}", null, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(tutor, HttpMethod.Get, $"/api/turnos/{turno.Id}/servicios", null, null, HttpStatusCode.OK);
        await Codigo(tutor, HttpMethod.Post, $"/api/turnos/{turno.Id}/cancelar", null, tokenTutor, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Put, $"/api/turnos/{turno.Id}/servicios/{s1}", new { completado = true }, tokenVet, HttpStatusCode.BadRequest);
        await Codigo(vet, HttpMethod.Delete, $"/api/perros/{perroNuevo.Id}", null, tokenVet, HttpStatusCode.NoContent);
        await Codigo(tutor, HttpMethod.Get, $"/api/perros/{perroNuevo.Id}", null, null, HttpStatusCode.NotFound);
        Exigir((await tutor.GetFromJsonAsync<List<TurnoRespuestaDto>>($"/api/perros/{perroNuevo.Id}/historial"))!.Any(x => x.Id == turno.Id), "La baja lógica borró el historial.");
        await Codigo(vet, HttpMethod.Delete, $"/api/tutores/{nuevoTutor.Id}", null, tokenVet, HttpStatusCode.NoContent);
        await Codigo(vet, HttpMethod.Get, $"/api/tutores/{nuevoTutor.Id}", null, null, HttpStatusCode.NotFound);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var compartido = await db.Turnos.Where(x => x.VeterinarioId == null && (x.EstadoId == EstadosTurno.Solicitado || x.EstadoId == EstadosTurno.Confirmado))
                .Select(x => new { x.Id, ServicioId = db.ServiciosPorTurno.Where(s => s.TurnoId == x.Id).Select(s => s.ServicioId).First() }).FirstAsync();
            await Codigo(vet, HttpMethod.Get, $"/api/turnos/{compartido.Id}/servicios", null, null, HttpStatusCode.OK);
            await Codigo(otroVet, HttpMethod.Get, $"/api/turnos/{compartido.Id}/servicios", null, null, HttpStatusCode.OK);
            await Codigo(otroVet, HttpMethod.Put, $"/api/turnos/{compartido.Id}/servicios/{compartido.ServicioId}", new { completado = false }, tokenOtroVet, HttpStatusCode.NoContent);
            var veto = await db.Vetos.Select(x => new { x.Id, x.TutorId }).FirstAsync();
            await Codigo(vet, HttpMethod.Get, $"/api/tutores/{veto.TutorId}/vetos/{veto.Id}", null, null, HttpStatusCode.OK);
            await Codigo(vet, HttpMethod.Get, $"/api/tutores/{int.MaxValue}/vetos/{veto.Id}", null, null, HttpStatusCode.NotFound);
            await Codigo(tutor, HttpMethod.Get, $"/api/tutores/{veto.TutorId}/vetos/{veto.Id}", null, null, HttpStatusCode.Forbidden);
        }
        Console.WriteLine("Cobertura API: CRUDs de perfiles, perros, teléfonos, patologías, catálogos, cupos y servicios; historial, roles activos, pertenencia, bajas, validaciones y antifalsificación verificados.");
    }
}
