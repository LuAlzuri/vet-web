using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static class AtencionPruebas
{
    private static void Exigir(bool cumple, string mensaje)
    { if (!cumple) throw new Exception("Atención: " + mensaje); }

    public static async Task EjecutarAsync(ServiceProvider provider, HttpClient vet, HttpClient otroVet, HttpClient tutor,
        HttpClient otroTutor, HttpClient admin, HttpClient anon, int perroId, string tokenVet, string tokenOtroVet, string tokenTutor)
    {
        int s1, s2, t1, t2; string usuarioVet, usuarioTutor; int perfilVet;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var servicios = new[] { new Servicio { Descripcion = "Atención baño" }, new Servicio { Descripcion = "Atención corte" } };
            var tratamientos = new[] { new Tratamiento { Descripcion = "Atención calmante" }, new Tratamiento { Descripcion = "Atención piel" } };
            db.AddRange(servicios); db.AddRange(tratamientos); await db.SaveChangesAsync();
            s1 = servicios[0].Id; s2 = servicios[1].Id; t1 = tratamientos[0].Id; t2 = tratamientos[1].Id;
            usuarioVet = await db.Users.Where(x => x.Email == SemillaDemo.EmailVeterinario).Select(x => x.Id).SingleAsync();
            perfilVet = await db.Veterinarios.Where(x => x.UserId == usuarioVet).Select(x => x.Id).SingleAsync();
            usuarioTutor = await db.Perros.Where(x => x.Id == perroId).Select(x => x.Tutor.UserId).SingleAsync();
        }
        async Task<int> Crear(bool compartido = false)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cupo = new CupoDiario { FechaCupo = (await db.CuposDiarios.MaxAsync(x => x.FechaCupo)).AddDays(1), NumeroCuposDisponibles = 6 };
            db.CuposDiarios.Add(cupo); await db.SaveChangesAsync();
            var servicio = scope.ServiceProvider.GetRequiredService<TurnoService>();
            // El tutor demo tiene una cancelación desde su último reinicio, pero no veto.
            var id = await servicio.GuardarAsync(new TurnoDto { PerroId = perroId, CupoDiarioId = cupo.Id,
                VeterinarioId = compartido ? null : perfilVet, ServicioIds = [s1, s2] }, usuarioTutor, false);
            await servicio.CambiarEstadoAsync(id, EstadosTurno.Confirmado, usuarioVet, true, 2);
            return id;
        }
        async Task FechaPasada(int id)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.CuposDiarios.Where(x => db.Turnos.Any(t => t.Id == id && t.CupoDiarioId == x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaCupo, Reloj.Hoy.AddDays(-1)));
        }
        async Task<HttpResponseMessage> Post(HttpClient cliente, int id, object datos, string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/turnos/{id}/completar") { Content = JsonContent.Create(datos) };
            request.Headers.Add("RequestVerificationToken", token);
            return await cliente.SendAsync(request);
        }
        var id = await Crear();
        using var futura = await Post(vet, id, new CompletarTurnoDto(), tokenVet);
        Exigir(futura.StatusCode == HttpStatusCode.BadRequest && (await futura.Content.ReadAsStringAsync()).Contains("antes de su fecha"), "se completó un turno futuro por API.");
        using var futuraWeb = await vet.GetAsync($"/Turnos/ObtenerAtencion?id={id}");
        Exigir(futuraWeb.StatusCode == HttpStatusCode.BadRequest, "la web permitió iniciar el cierre de un turno futuro.");
        await FechaPasada(id);
        using var ajena = await Post(otroVet, id, new CompletarTurnoDto(), tokenOtroVet);
        Exigir(ajena.StatusCode == HttpStatusCode.Forbidden, "un veterinario completó un turno asignado a otro.");
        using var comoTutor = await Post(tutor, id, new CompletarTurnoDto(), tokenTutor);
        Exigir(comoTutor.StatusCode == HttpStatusCode.Forbidden, "un tutor registró una atención.");
        using var sinToken = await vet.PostAsJsonAsync($"/api/turnos/{id}/completar", new CompletarTurnoDto());
        Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "se guardó sin antifalsificación.");
        foreach (var invalido in new object[] {
            new CompletarTurnoDto { ServicioIdsCompletados = [s1, s1] },
            new CompletarTurnoDto { ServicioIdsCompletados = [int.MaxValue] },
            new CompletarTurnoDto { Tratamientos = [new() { TratamientoId = t1 }, new() { TratamientoId = t1 }] },
            new CompletarTurnoDto { Tratamientos = [new() { TratamientoId = int.MaxValue }] },
            new CompletarTurnoDto { Tratamientos = [new() { TratamientoId = t1, Razon = new string('x', 1001) }] },
            new CompletarTurnoDto { Observaciones = new string('x', 2001) },
            new { servicioIdsCompletados = (int[]?)null, tratamientos = Array.Empty<object>() },
            new { servicioIdsCompletados = Array.Empty<int>(), tratamientos = new object?[] { null } }
        })
        {
            using var respuesta = await Post(vet, id, invalido, tokenVet);
            Exigir(respuesta.StatusCode == HttpStatusCode.BadRequest, "se aceptaron datos clínicos inválidos.");
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var turno = await db.Turnos.Include(x => x.CupoDiario).SingleAsync(x => x.Id == id);
            Exigir(turno.EstadoId == EstadosTurno.Confirmado && turno.CupoDiario.NumeroCuposDisponibles == 4
                && !await db.ServiciosPorTurno.AnyAsync(x => x.TurnoId == id && x.Completado)
                && !await db.TratamientosPorTurno.AnyAsync(x => x.TurnoId == id), "un intento rechazado dejó efectos parciales.");
        }
        var formulario = await vet.GetStringAsync($"/Turnos/ObtenerAtencion?id={id}");
        var carpeta = Environment.GetEnvironmentVariable("VETWEB_ATENCION_VERIFICACION");
        if (!string.IsNullOrWhiteSpace(carpeta))
        {
            Directory.CreateDirectory(carpeta);
            await File.WriteAllTextAsync(Path.Combine(carpeta, "formulario.html"), formulario);
        }
        Exigir(formulario.Contains("ServicioIdsCompletados") && formulario.Contains("Tratamientos.Index"), "falta el formulario completo de atención.");
        var token = WebUtility.HtmlDecode(Regex.Match(formulario, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var guardada = await vet.PostAsync("/Turnos/Completar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Id"] = id.ToString(), ["ServicioIdsCompletados"] = s1.ToString(),
            ["Tratamientos.Index"] = t1.ToString(), [$"Tratamientos[{t1}].TratamientoId"] = t1.ToString(),
            [$"Tratamientos[{t1}].Razon"] = "  Inquietud durante el baño  ", ["Observaciones"] = "  Corte pendiente  "
        }.Concat(new[] {
            new KeyValuePair<string, string>("Tratamientos.Index", t2.ToString()),
            new KeyValuePair<string, string>($"Tratamientos[{t2}].TratamientoId", t2.ToString()),
            new KeyValuePair<string, string>($"Tratamientos[{t2}].Razon", "Piel sensible")
        })));
        Exigir(guardada.IsSuccessStatusCode && (await guardada.Content.ReadAsStringAsync()).Contains("true"), "el formulario no guardó los tratamientos múltiples.");
        var atencion = (await tutor.GetFromJsonAsync<AtencionRespuestaDto>($"/api/turnos/{id}/atencion"))!;
        Exigir(atencion.EstadoId == EstadosTurno.Completado && atencion.FinalizacionUtc.HasValue
            && atencion.ServicioIdsCompletados.SequenceEqual([s1]) && atencion.Tratamientos.Count == 2
            && atencion.Tratamientos[0].Razon == "Inquietud durante el baño" && atencion.Observaciones == "Corte pendiente",
            "el historial API perdió datos o no normalizó los textos.");
        foreach (var cliente in new[] { otroVet, otroTutor })
        {
            using var privado = await cliente.GetAsync($"/api/turnos/{id}/atencion");
            Exigir(privado.StatusCode == HttpStatusCode.NotFound, "se expuso la atención de un recurso ajeno.");
        }
        Exigir((await admin.GetAsync($"/api/turnos/{id}/atencion")).StatusCode == HttpStatusCode.Forbidden
            && (await anon.GetAsync($"/api/turnos/{id}/atencion")).StatusCode == HttpStatusCode.Unauthorized, "falló la protección de lectura de atención.");
        using var repetida = await Post(vet, id, new CompletarTurnoDto(), tokenVet);
        Exigir(repetida.StatusCode == HttpStatusCode.BadRequest, "se completó dos veces.");
        var historial = WebUtility.HtmlDecode(await tutor.GetStringAsync($"/Turnos/Historial?perroId={perroId}&personal=true"));
        Exigir(historial.Contains("Inquietud durante el baño") && historial.Contains("Piel sensible"), "faltan motivos en el historial web.");
        var periodo = $"?desde={Reloj.Hoy:yyyy-MM-dd}&hasta={Reloj.Hoy:yyyy-MM-dd}";
        var incompletos = (await admin.GetFromJsonAsync<InformeDto>("/api/reportes/servicios-incompletos/datos" + periodo))!;
        Exigir(incompletos.Atenciones.Any(x => x.TurnoId == id && x.Completados == 1 && x.Servicios == 2), "el cierre no alimentó el informe de incompletos por su fecha real.");
        var compartido = await Crear(compartido: true);
        await FechaPasada(compartido);
        var datos = new CompletarTurnoDto { ServicioIdsCompletados = [s1, s2], Tratamientos = [new() { TratamientoId = t2, Razon = "Atención compartida" }] };
        // Ambos profesionales pueden atender No especifica; solo uno puede cerrar el turno.
        var cierres = await Task.WhenAll(Post(vet, compartido, datos, tokenVet), Post(otroVet, compartido, datos, tokenOtroVet));
        try
        {
            Exigir(cierres.Count(x => x.StatusCode == HttpStatusCode.NoContent) == 1
                && cierres.Count(x => x.StatusCode == HttpStatusCode.BadRequest) == 1,
                "dos cierres concurrentes prosperaron o se bloqueó No especifica.");
        }
        finally { foreach (var respuesta in cierres) respuesta.Dispose(); }
        var completos = (await admin.GetFromJsonAsync<InformeDto>("/api/reportes/servicios-completos/datos" + periodo))!;
        Exigir(completos.Atenciones.Any(x => x.TurnoId == compartido), "el cierre no alimentó el informe de completos.");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Exigir(await db.Turnos.Where(x => x.Id == id).Select(x => x.CupoDiario.NumeroCuposDisponibles).SingleAsync() == 4,
                "completar devolvió o volvió a consumir plazas.");
            Exigir(await db.AuditoriaAcciones.CountAsync(x => x.Entidad == nameof(Turno) && x.EntidadId == id
                && x.EstadoNuevoId == EstadosTurno.Completado) == 1, "se duplicó la auditoría de finalización.");
        }
        // Un fallo al guardar el evento de finalización revierte todo el registro clínico.
        var fallo = await Crear(); await FechaPasada(fallo);
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_cierre_atencion() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Accion" = 'EstadoTurnoCambiado' AND NEW."EstadoNuevoId" = 4 THEN
                        RAISE EXCEPTION 'Fallo intencional de auditoría de atención';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER fallar_cierre_atencion BEFORE INSERT ON "AuditoriaAcciones"
                    FOR EACH ROW EXECUTE FUNCTION fallar_cierre_atencion();
                """);
        try
        {
            using var respuesta = await Post(vet, fallo, datos, tokenVet);
            Exigir(respuesta.StatusCode == HttpStatusCode.BadRequest, "no se provocó el fallo de auditoría esperado.");
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Exigir(await db.Turnos.Where(x => x.Id == fallo).Select(x => x.EstadoId).SingleAsync() == EstadosTurno.Confirmado
                && !await db.TratamientosPorTurno.AnyAsync(x => x.TurnoId == fallo)
                && !await db.ServiciosPorTurno.AnyAsync(x => x.TurnoId == fallo && x.Completado)
                && !await db.AuditoriaAcciones.AnyAsync(x => x.Entidad == nameof(Turno) && x.EntidadId == fallo && x.EstadoNuevoId == EstadosTurno.Completado),
                "el fallo no revirtió estado, servicios, tratamientos y auditoría.");
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("""
                DROP TRIGGER fallar_cierre_atencion ON "AuditoriaAcciones";
                DROP FUNCTION fallar_cierre_atencion();
                """);
        }
        using var reintento = await Post(vet, fallo, datos, tokenVet);
        Exigir(reintento.StatusCode == HttpStatusCode.NoContent, "no se pudo reintentar el cierre tras rollback.");
        Console.WriteLine("Atención: fecha mínima, servicios y tratamientos múltiples con motivos, formulario web, API, privacidad, antifalsificación, intentos inválidos, plazas, concurrencia, rollback e informes verificados.");
    }
}
