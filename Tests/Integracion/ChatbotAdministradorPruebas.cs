using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static class ChatbotAdministradorPruebas
{
    private static void Exigir(bool condicion, string mensaje)
    { if (!condicion) throw new Exception("Chatbot Administrador: " + mensaje); }
    private static DateTime Utc(DateTime valor) => TimeZoneInfo.ConvertTimeToUtc(valor, TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));

    public static async Task EjecutarAsync(ServiceProvider provider, HttpClient admin, HttpClient vet, HttpClient tutor,
        HttpClient anonimo, HttpClient multirrolConVetActivo)
    {
        var tokens = new Dictionary<HttpClient, string>();
        foreach (var cliente in new[] { admin, vet, tutor, multirrolConVetActivo })
        {
            var html = await cliente.GetStringAsync("/");
            tokens[cliente] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        async Task<HttpResponseMessage> Enviar(HttpClient cliente, ChatbotAccionSolicitud solicitud, bool usarToken = true)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/acciones") { Content = JsonContent.Create(solicitud) };
            if (usarToken && tokens.TryGetValue(cliente, out var token)) peticion.Headers.Add("RequestVerificationToken", token);
            return await cliente.SendAsync(peticion);
        }
        async Task<ChatbotAccionRespuesta> Consultar(ChatbotAccionSolicitud solicitud)
        {
            using var respuesta = await Enviar(admin, solicitud);
            Exigir(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
            Exigir(respuesta.Headers.CacheControl?.NoStore == true, "respuesta cacheable");
            return (await respuesta.Content.ReadFromJsonAsync<ChatbotAccionRespuesta>())!;
        }
        var inicio = await Consultar(new());
        var nombres = new[] { "admin-servicios", "admin-estados", "admin-comparar", "admin-auditoria" };
        Exigir(inicio.Acciones.Select(x => x.Solicitud.Accion).Order().SequenceEqual(nombres.Order()), "menú incorrecto");
        foreach (var nombre in nombres)
            foreach (var cliente in new[] { tutor, vet, multirrolConVetActivo })
            {
                using var denegada = await Enviar(cliente, new() { Accion = nombre });
                Exigir(denegada.StatusCode == HttpStatusCode.Forbidden, "filtración a otro rol activo: " + nombre);
            }
        foreach (var nombre in new[] { "preparar", "confirmar", "turnos", "vetos" })
        {
            using var denegada = await Enviar(admin, new() { Accion = nombre });
            Exigir(denegada.StatusCode == HttpStatusCode.Forbidden, "Administrador obtuvo herramientas de otro rol");
        }
        using var sinSesion = await Enviar(anonimo, new() { Accion = "admin-auditoria" });
        Exigir(sinSesion.StatusCode == HttpStatusCode.Unauthorized, "anónimo admitido");
        using var sinToken = await Enviar(admin, new(), false);
        Exigir(sinToken.StatusCode == HttpStatusCode.BadRequest, "sin CSRF");
        using var periodoInvalido = await Enviar(admin, new() { Accion = "admin-estados", Periodo = "cualquier cosa" });
        Exigir(periodoInvalido.StatusCode == HttpStatusCode.BadRequest, "período inválido");

        async Task<Dictionary<string, int>> Totales(string periodo)
        {
            var respuesta = await Consultar(new() { Accion = "admin-estados", Periodo = periodo });
            return respuesta.Tablas.Single().Filas.ToDictionary(x => x[0], x => int.Parse(x[1]));
        }
        var antesActual = await Totales("mes-actual");
        var antesAnterior = await Totales("mes-anterior");
        var hoy = Reloj.Hoy; var actual = new DateTime(hoy.Year, hoy.Month, 1); var anterior = actual.AddMonths(-1);
        var inicioActual = Utc(actual); var inicioAnterior = Utc(anterior);
        long eventosAntesConsultas; int turnosAntesConsultas, cuposAntesConsultas;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tutorPerfil = await db.Tutores.SingleAsync(x => x.Usuario.Email == "chatA@prueba.invalid");
            var usuarioVet = await db.Veterinarios.Where(x => x.Usuario.Email == SemillaDemo.EmailVeterinario).Select(x => x.UserId).SingleAsync();
            var perro = new Perro { Nombre = "Historia administrativa", TutorId = tutorPerfil.Id, GrupoEtarioId = await db.GruposEtarios.Select(x => x.Id).FirstAsync(),
                TamanioId = await db.Tamanios.Select(x => x.Id).FirstAsync(), FechaNacimiento = hoy.AddYears(-2) };
            var a = new Servicio { Descripcion = "Ranking admin A" }; var b = new Servicio { Descripcion = "Ranking admin B" };
            var cupo = new CupoDiario { FechaCupo = hoy.AddYears(1), NumeroCuposDisponibles = 12 };
            db.AddRange(perro, a, b, cupo); await db.SaveChangesAsync();
            var turnos = new[] {
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.Completado },
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.CanceladoTutor },
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.CanceladoVeterinario },
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.Solicitado },
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.Solicitado },
                new Turno { PerroId = perro.Id, CupoDiarioId = cupo.Id, EstadoId = EstadosTurno.Solicitado }
            };
            db.Turnos.AddRange(turnos); await db.SaveChangesAsync();
            foreach (var i in new[] { 0, 1, 2, 5 }) db.ServiciosPorTurno.Add(new ServicioPorTurno { TurnoId = turnos[i].Id, ServicioId = a.Id });
            foreach (var i in new[] { 1, 3, 4 }) db.ServiciosPorTurno.Add(new ServicioPorTurno { TurnoId = turnos[i].Id, ServicioId = b.Id });
            void Evento(int i, DateTime fecha, string accion, int estado) => db.AuditoriaAcciones.Add(new AuditoriaAccion
            { FechaUtc = fecha, UsuarioId = usuarioVet, RolActivo = "Veterinario", TutorId = tutorPerfil.Id, Entidad = nameof(Turno), EntidadId = turnos[i].Id, Accion = accion, EstadoNuevoId = estado });
            Evento(0, inicioAnterior, AuditoriaAccion.TurnoSolicitado, EstadosTurno.Solicitado);
            Evento(0, inicioActual, AuditoriaAccion.EstadoTurnoCambiado, EstadosTurno.Completado);
            Evento(0, inicioActual.AddMinutes(1), AuditoriaAccion.EstadoTurnoCambiado, EstadosTurno.Completado); // no duplicar
            Evento(1, inicioActual, AuditoriaAccion.ReservaPresencial, EstadosTurno.Solicitado);
            Evento(1, inicioActual.AddMinutes(1), AuditoriaAccion.TurnoSolicitado, EstadosTurno.Solicitado); // primera creación
            Evento(1, inicioActual, AuditoriaAccion.EstadoTurnoCambiado, EstadosTurno.CanceladoTutor);
            Evento(2, inicioActual.AddTicks(-10), AuditoriaAccion.TurnoSolicitado, EstadosTurno.Solicitado);
            Evento(2, inicioActual.AddTicks(-10), AuditoriaAccion.EstadoTurnoCambiado, EstadosTurno.CanceladoVeterinario);
            Evento(3, Utc(actual.AddMonths(1)), AuditoriaAccion.TurnoSolicitado, EstadosTurno.Solicitado);
            Evento(4, inicioAnterior.AddTicks(-10), AuditoriaAccion.TurnoSolicitado, EstadosTurno.Solicitado);
            perro.Eliminado = true; // los informes históricos conservan las bajas lógicas
            await db.SaveChangesAsync();
            eventosAntesConsultas = await db.AuditoriaAcciones.LongCountAsync();
            turnosAntesConsultas = await db.Turnos.IgnoreQueryFilters().CountAsync();
            cuposAntesConsultas = await db.CuposDiarios.SumAsync(x => x.NumeroCuposDisponibles);
        }
        var despuesActual = await Totales("mes-actual"); var despuesAnterior = await Totales("mes-anterior");
        foreach (var indicador in antesActual.Keys)
        {
            var delta = indicador is "Solicitudes registradas" or "Turnos completados" or "Cancelados por el tutor" or "Total de turnos cancelados" ? 1 : 0;
            Exigir(despuesActual[indicador] == antesActual[indicador] + delta, "fecha real o deduplicación actual: " + indicador);
            var deltaAnterior = indicador == "Solicitudes registradas" ? 2 : indicador is "Cancelados por el veterinario" or "Total de turnos cancelados" ? 1 : 0;
            Exigir(despuesAnterior[indicador] == antesAnterior[indicador] + deltaAnterior, "límite argentino anterior: " + indicador);
        }
        async Task<Dictionary<string, int>> Ranking(string periodo)
        {
            var filas = new Dictionary<string, int>(); var pagina = 1;
            while (true)
            {
                var respuesta = await Consultar(new() { Accion = "admin-servicios", Periodo = periodo, Pagina = pagina });
                foreach (var fila in respuesta.Tablas.Single().Filas) filas.Add(fila[0], int.Parse(fila[1]));
                if (!respuesta.Acciones.Any(x => x.Etiqueta == "Página siguiente")) break;
                pagina++; Exigir(pagina < 100, "paginación sin fin");
            }
            return filas;
        }
        var rankingActual = await Ranking("mes-actual"); var rankingAnterior = await Ranking("mes-anterior");
        Exigir(rankingActual["Ranking admin A"] == 1 && rankingActual["Ranking admin B"] == 1, "ranking actual incluye solicitudes fuera del período o sin auditoría");
        Exigir(rankingAnterior["Ranking admin A"] == 2 && !rankingAnterior.ContainsKey("Ranking admin B"), "ranking anterior incorrecto");
        var comparar = await Consultar(new() { Accion = "admin-comparar" });
        Exigir(comparar.Mensaje.Contains("en curso") && comparar.Mensaje.Contains("Cobertura"), "no explica períodos/cobertura");
        var comparacion = comparar.Tablas.Single().Filas.ToDictionary(x => x[0]);
        Exigir(int.Parse(comparacion["Solicitudes"][1]) == despuesAnterior["Solicitudes registradas"]
            && int.Parse(comparacion["Solicitudes"][2]) == despuesActual["Solicitudes registradas"], "comparación no coincide con totales");
        Exigir(int.Parse(comparacion["Completados"][3]) == despuesActual["Turnos completados"] - despuesAnterior["Turnos completados"], "diferencia incorrecta");
        var auditoria = await Consultar(new() { Accion = "admin-auditoria" });
        Exigir(auditoria.Tablas.Single().Filas.Count == 25 && auditoria.Acciones.Any(x => x.Etiqueta == "Página siguiente"), "auditoría no paginada");
        var pagina2 = await Consultar(new() { Accion = "admin-auditoria", Pagina = 2 });
        Exigir(!auditoria.Tablas.Single().Filas.Select(x => x[0]).Intersect(pagina2.Tablas.Single().Filas.Select(x => x[0])).Any(), "auditoría duplica páginas");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var esperados = await db.AuditoriaAcciones.IgnoreQueryFilters().OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id).Take(25).Select(x => x.Id.ToString()).ToListAsync();
            Exigir(auditoria.Tablas.Single().Filas.Select(x => x[0]).SequenceEqual(esperados), "orden de auditoría incorrecto");
            Exigir(eventosAntesConsultas == await db.AuditoriaAcciones.LongCountAsync()
                && turnosAntesConsultas == await db.Turnos.IgnoreQueryFilters().CountAsync()
                && cuposAntesConsultas == await db.CuposDiarios.SumAsync(x => x.NumeroCuposDisponibles), "una consulta modificó datos");
        }
        Console.WriteLine("Chatbot Administrador: cuatro consultas exclusivas del rol activo, límites mensuales argentinos, eventos deduplicados, bajas lógicas, ranking, comparación, auditoría paginada, CSRF y solo lectura verificados.");
    }
}
