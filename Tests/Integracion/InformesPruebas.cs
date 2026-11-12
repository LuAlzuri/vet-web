using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static class InformesPruebas
{
    private static void Exigir(bool cumple, string mensaje)
    {
        if (!cumple) throw new Exception("Informes: " + mensaje);
    }

    public static async Task EjecutarAsync(ServiceProvider provider, string password, HttpClient administrador,
        HttpClient tutor, HttpClient veterinario, HttpClient anonimo, HttpClient cuentaAdminConRolVeterinario)
    {
        var inicio = new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddDays(1);
        var ids = new List<int>();
        await using (var scope = provider.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            await sp.GetRequiredService<CuentaService>().GuardarAsync(new CuentaDto
            { Nombre = "Tutor", Apellido = "Informes", Email = "informes@prueba.invalid", Password = password }, false);
            var perfil = await db.Tutores.SingleAsync(x => x.Usuario.Email == "informes@prueba.invalid");
            var perro = new Perro { Nombre = "Perro informe", TutorId = perfil.Id, FechaNacimiento = Reloj.Hoy.AddYears(-3),
                GrupoEtarioId = await db.GruposEtarios.Select(x => x.Id).FirstAsync(),
                TamanioId = await db.Tamanios.Select(x => x.Id).FirstAsync() };
            var cupo = new CupoDiario { FechaCupo = new DateTime(2035, 7, 1), NumeroCuposDisponibles = 20 };
            var servicios = new[] { new Servicio { Descripcion = "Informe baño" }, new Servicio { Descripcion = "Informe corte" } };
            db.AddRange(perro, cupo); db.AddRange(servicios); await db.SaveChangesAsync();
            db.PerrosPatologias.Add(new PerroPatologia { PerroId = perro.Id,
                Patologia = new Patologia { Descripcion = "Dermatitis de informe" } });
            await db.SaveChangesAsync();
            async Task Agregar(DateTime solicitud, DateTime finalizacion, int estado, string? observaciones,
                bool auditar = true, bool presencial = false, params bool[] realizados)
            {
                var turno = new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = estado, Observaciones = observaciones };
                db.Turnos.Add(turno); await db.SaveChangesAsync(); ids.Add(turno.Id);
                for (var i = 0; i < realizados.Length; i++)
                    db.ServiciosPorTurno.Add(new ServicioPorTurno { TurnoId = turno.Id, ServicioId = servicios[i].Id, Completado = realizados[i] });
                if (auditar)
                {
                    db.AuditoriaAcciones.Add(new AuditoriaAccion { FechaUtc = solicitud, UsuarioId = perfil.UserId,
                        TutorId = perfil.Id, RolActivo = presencial ? "Veterinario" : "Tutor",
                        Accion = presencial ? AuditoriaAccion.ReservaPresencial : AuditoriaAccion.TurnoSolicitado,
                        Entidad = nameof(Turno), EntidadId = turno.Id, EstadoNuevoId = EstadosTurno.Solicitado });
                    db.AuditoriaAcciones.Add(new AuditoriaAccion { FechaUtc = finalizacion, UsuarioId = perfil.UserId,
                        TutorId = perfil.Id, RolActivo = "Veterinario", Accion = AuditoriaAccion.EstadoTurnoCambiado,
                        Entidad = nameof(Turno), EntidadId = turno.Id, EstadoAnteriorId = EstadosTurno.Confirmado, EstadoNuevoId = estado });
                }
                await db.SaveChangesAsync();
            }
            await Agregar(inicio.AddMinutes(-1), inicio.AddHours(10), EstadosTurno.Completado, null, realizados: [true]);
            await Agregar(inicio, fin.AddTicks(-10), EstadosTurno.Completado, "Pendientes ambos servicios", realizados: [false, false]);
            await Agregar(inicio.AddHours(1), inicio.AddHours(11), EstadosTurno.Completado, "Corte realizado; baño pendiente", realizados: [true, false]);
            await Agregar(inicio.AddHours(2), inicio.AddHours(12), EstadosTurno.Completado, "Sin servicios");
            await Agregar(inicio.AddHours(3), inicio.AddHours(13), EstadosTurno.Rechazado, "No debe aparecer en atenciones", realizados: [false]);
            await Agregar(fin, fin, EstadosTurno.Completado, "Día siguiente", realizados: [false]);
            await Agregar(inicio.AddHours(4), inicio.AddHours(14), EstadosTurno.Completado, "Sin eventos históricos", auditar: false, realizados: [true]);
            await Agregar(inicio.AddHours(5), inicio.AddHours(15), EstadosTurno.CanceladoVeterinario, "Reserva presencial cancelada", presencial: true, realizados: [false]);
            // Tampoco debe multiplicar solicitudes una duplicación de eventos de creación.
            db.AuditoriaAcciones.Add(new AuditoriaAccion { FechaUtc = inicio.AddHours(6), UsuarioId = perfil.UserId,
                TutorId = perfil.Id, RolActivo = "Tutor", Accion = AuditoriaAccion.TurnoSolicitado,
                Entidad = nameof(Turno), EntidadId = ids[1], EstadoNuevoId = EstadosTurno.Solicitado });
            perro.Eliminado = true; perfil.Eliminado = true;
            await db.SaveChangesAsync();
        }
        const string periodo = "?desde=2026-03-10&hasta=2026-03-10";
        var frecuencia = (await administrador.GetFromJsonAsync<InformeDto>("/api/reportes/frecuencia-solicitudes/datos" + periodo))!;
        Exigir(frecuencia.Total == 5 && frecuencia.Solicitudes.SequenceEqual([new(new DateOnly(2026, 3, 10), 5)]),
            "el período, reservas presenciales o conteo único de solicitudes son incorrectos.");
        Exigir(frecuencia.AuditoriaDesdeUtc == inicio.AddMinutes(-1), "no se informó la primera fecha de auditoría.");
        var completos = (await administrador.GetFromJsonAsync<InformeDto>("/api/reportes/servicios-completos/datos" + periodo))!;
        Exigir(completos.Total == 1 && completos.Atenciones.Single().TurnoId == ids[0],
            "completos incluyó servicios vacíos, sin auditoría, fuera del período o perdió bajas lógicas.");
        var incompletos = (await administrador.GetFromJsonAsync<InformeDto>("/api/reportes/servicios-incompletos/datos" + periodo))!;
        Exigir(incompletos.Total == 2 && incompletos.Atenciones.Select(x => x.TurnoId).SequenceEqual([ids[2], ids[1]])
            && incompletos.Patologias.SequenceEqual([new PatologiaInformeDto("Dermatitis de informe", 2)]),
            "la grilla de incompletos o el conteo de patologías por atención es incorrecto.");
        Exigir(incompletos.Atenciones.All(x => x.Observaciones!.Contains("pendiente", StringComparison.OrdinalIgnoreCase)), "faltan observaciones.");
        var anterior = (await administrador.GetFromJsonAsync<InformeDto>("/api/reportes/frecuencia-solicitudes/datos?desde=2026-03-09&hasta=2026-03-09"))!;
        Exigir(anterior.Total == 1 && anterior.Solicitudes.Single().Fecha == new DateOnly(2026, 3, 9), "se agrupó por UTC en vez de Argentina.");
        var vacio = (await administrador.GetFromJsonAsync<InformeDto>("/api/reportes/servicios-incompletos/datos?desde=2025-01-01&hasta=2025-01-01"))!;
        Exigir(vacio.Total == 0 && vacio.Atenciones.Count == 0 && vacio.Patologias.Count == 0, "el período vacío contiene datos.");

        var reportes = new[] { "frecuencia-solicitudes", "servicios-completos", "servicios-incompletos" };
        for (var i = 0; i < reportes.Length; i++)
        {
            var reporte = reportes[i];
            foreach (var cliente in new[] { tutor, veterinario, cuentaAdminConRolVeterinario })
                foreach (var sufijo in new[] { "", "/datos" })
                {
                    using var prohibido = await cliente.GetAsync($"/api/reportes/{reporte}{sufijo}" + periodo);
                    Exigir(prohibido.StatusCode == HttpStatusCode.Forbidden, "un rol no activo de Administrador accedió al informe.");
                }
            foreach (var ruta in new[] { $"/api/reportes/{reporte}", $"/api/reportes/{reporte}/datos" })
            {
                using var sinSesion = await anonimo.GetAsync(ruta + periodo);
                Exigir(sinSesion.StatusCode == HttpStatusCode.Unauthorized, "un anónimo obtuvo datos de informe.");
            }
            using var pdf = await administrador.GetAsync($"/api/reportes/{reporte}" + periodo);
            Exigir(pdf.StatusCode == HttpStatusCode.OK && pdf.Content.Headers.ContentType?.MediaType == "application/pdf"
                && pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"') == reporte + ".pdf", "no se descargó PDF con nombre y tipo correctos.");
            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            Exigir(bytes.AsSpan().StartsWith("%PDF-"u8) && bytes.Length > 1000, "la descarga no contiene un PDF generado.");
            var carpeta = Environment.GetEnvironmentVariable("VETWEB_PDF_VERIFICACION");
            if (!string.IsNullOrWhiteSpace(carpeta))
            {
                Directory.CreateDirectory(carpeta);
                await File.WriteAllBytesAsync(Path.Combine(carpeta, reporte + ".pdf"), bytes);
            }
            var rutaWeb = $"/Informes/Listado?Tipo={i}&Periodo.Desde=2026-03-10&Periodo.Hasta=2026-03-10";
            using var grilla = await administrador.GetAsync(rutaWeb);
            var html = WebUtility.HtmlDecode(await grilla.Content.ReadAsStringAsync());
            Exigir(grilla.IsSuccessStatusCode && html.Contains(InformesService.Titulo((TipoInforme)i))
                && html.Contains("Descargar PDF") && html.Contains("Primer evento de auditoría registrado: 09/03/2026 23:59:00"),
                "la grilla no muestra título, descarga o cobertura de auditoría.");
            if (!string.IsNullOrWhiteSpace(carpeta))
                await File.WriteAllTextAsync(Path.Combine(carpeta, reporte + ".html"), await grilla.Content.ReadAsStringAsync());
            using var pdfWeb = await administrador.GetAsync($"/Informes/Pdf?tipo={i}&desde=2026-03-10&hasta=2026-03-10");
            Exigir(pdfWeb.IsSuccessStatusCode && (await pdfWeb.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8), "la descarga web no genera PDF.");
        }
        foreach (var cliente in new[] { tutor, veterinario, cuentaAdminConRolVeterinario, anonimo })
            foreach (var ruta in new[] { "/Informes/Listado", "/Informes/Pdf?tipo=0&desde=2026-03-10&hasta=2026-03-10" })
            {
                using var denegado = await cliente.GetAsync(ruta);
                Exigir(denegado.StatusCode == HttpStatusCode.Redirect, "un rol ajeno obtuvo la pantalla o PDF web.");
            }
        foreach (var fechas in new[] { "", "?desde=2026-03-11&hasta=2026-03-10", "?desde=inválida&hasta=2026-03-10",
            "?desde=2026-03-10", "?desde=2026-03-10&hasta=9999-12-31" })
        {
            using var invalido = await administrador.GetAsync("/api/reportes/frecuencia-solicitudes/datos" + fechas);
            Exigir(invalido.StatusCode == HttpStatusCode.BadRequest, "no se validaron las fechas del período.");
        }
        using var sinDatosPdf = await administrador.GetAsync("/api/reportes/servicios-incompletos?desde=2025-01-01&hasta=2025-01-01");
        Exigir(sinDatosPdf.IsSuccessStatusCode && (await sinDatosPdf.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8), "no se generó PDF vacío.");
        using var inicioWeb = await administrador.GetAsync("/Informes/Listado");
        Exigir(inicioWeb.IsSuccessStatusCode, "falló el período por defecto.");
        using var errorWeb = await administrador.GetAsync("/Informes/Listado?Tipo=0&Periodo.Desde=2026-03-11&Periodo.Hasta=2026-03-10");
        Exigir(errorWeb.IsSuccessStatusCode && WebUtility.HtmlDecode(await errorWeb.Content.ReadAsStringAsync()).Contains("fecha inicial debe ser menor"), "la web no explica el período inválido.");
        using var tipoInvalido = await administrador.GetAsync("/Informes/Pdf?tipo=99&desde=2026-03-10&hasta=2026-03-10");
        Exigir(tipoInvalido.StatusCode == HttpStatusCode.BadRequest, "se aceptó un tipo inválido.");
        Console.WriteLine("Informes: fechas de auditoría y límites argentinos, bajas lógicas, totales, patologías, grillas, seis descargas PDF, períodos vacíos e inválidos y permisos por rol activo verificados.");
    }
}
