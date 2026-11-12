using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static class ChatbotAccionesPruebas
{
    private static void Exigir(bool condicion, string mensaje)
    { if (!condicion) throw new Exception("Acciones del chatbot: " + mensaje); }

    public static async Task EjecutarAsync(ServiceProvider provider, HttpClient veterinario, HttpClient administrador,
        HttpClient anonimo, Func<HttpClient> crearCliente, Func<HttpClient, string, Task> ingresar, string password)
    {
        int perroA, perroB, perroA2, tutorA, tutorB, cupo1, cupo2, cupo3, servicio, tratamiento;
        string usuarioA, usuarioVet;
        var fecha = Reloj.Hoy;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cuentas = scope.ServiceProvider.GetRequiredService<CuentaService>();
            foreach (var letra in new[] { "A", "B" })
                await cuentas.GuardarAsync(new CuentaDto { Nombre = "Chat" + letra, Apellido = "Pruebas", Email = $"chat{letra}@prueba.invalid", Password = password }, false);
            var a = await db.Tutores.Include(x => x.Usuario).SingleAsync(x => x.Usuario.Email == "chatA@prueba.invalid");
            var b = await db.Tutores.SingleAsync(x => x.Usuario.Email == "chatB@prueba.invalid");
            tutorA = a.Id; tutorB = b.Id; usuarioA = a.UserId;
            usuarioVet = await db.Veterinarios.Where(x => x.Usuario.Email == SemillaDemo.EmailVeterinario).Select(x => x.UserId).SingleAsync();
            var grupo = await db.GruposEtarios.FirstAsync(); var tamanio = await db.Tamanios.FirstAsync();
            Perro Perro(string nombre, int tutor) => new() { Nombre = nombre, TutorId = tutor, FechaNacimiento = Reloj.Hoy.AddYears(-2), GrupoEtarioId = grupo.Id, TamanioId = tamanio.Id };
            var pa = Perro("ChatLuna", tutorA); var pb = Perro("ChatPrivado", tutorB); var pa2 = Perro("ChatMilo", tutorA);
            var nuevoServicio = new Servicio { Descripcion = "Servicio chatbot" }; var nuevoTratamiento = new Tratamiento { Descripcion = "Tratamiento chatbot" };
            db.AddRange(pa, pb, pa2, nuevoServicio, nuevoTratamiento); await db.SaveChangesAsync();
            perroA = pa.Id; perroB = pb.Id; perroA2 = pa2.Id; servicio = nuevoServicio.Id; tratamiento = nuevoTratamiento.Id;
            fecha = (await db.CuposDiarios.MaxAsync(x => x.FechaCupo)).AddDays(1);
            if (fecha <= Reloj.Hoy) fecha = Reloj.Hoy.AddDays(1);
            var cupos = Enumerable.Range(0, 13).Select(i => new CupoDiario { FechaCupo = fecha.AddDays(i), NumeroCuposDisponibles = i == 12 ? 0 : 1 }).ToList();
            db.CuposDiarios.AddRange(cupos);
            var hoy = await db.CuposDiarios.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.FechaCupo == Reloj.Hoy);
            if (hoy is null) { hoy = new CupoDiario { FechaCupo = Reloj.Hoy, NumeroCuposDisponibles = 10 }; db.Add(hoy); }
            else hoy.NumeroCuposDisponibles += 10;
            var patologia = new Patologia { Descripcion = "Patología chatbot" }; db.Add(patologia);
            await db.SaveChangesAsync(); cupo1 = cupos[0].Id; cupo2 = cupos[1].Id; cupo3 = cupos[2].Id;
            db.PerrosPatologias.Add(new PerroPatologia { PerroId = perroA, PatologiaId = patologia.Id, Observacion = "Observación clínica privada", Controlado = true });
            await db.SaveChangesAsync();
            var turnos = scope.ServiceProvider.GetRequiredService<TurnoService>();
            var cancelado = await turnos.GuardarAsync(new TurnoDto { PerroId = perroA, CupoDiarioId = hoy.Id, ServicioIds = [servicio] }, usuarioA, false);
            await turnos.CambiarEstadoAsync(cancelado, EstadosTurno.CanceladoTutor, usuarioA, false);
            var completado = await turnos.GuardarAsync(new TurnoDto { PerroId = perroA, CupoDiarioId = hoy.Id, ServicioIds = [servicio] }, usuarioA, false);
            await turnos.CambiarEstadoAsync(completado, EstadosTurno.Confirmado, usuarioVet, true);
            await turnos.CompletarAsync(completado, new CompletarTurnoDto { ServicioIdsCompletados = [servicio], Observaciones = "Atención registrada por el veterinario", Tratamientos = [new() { TratamientoId = tratamiento, Razon = "Motivo clínico chatbot" }] }, usuarioVet);
            await turnos.GuardarAsync(new TurnoDto { PerroId = perroB, CupoDiarioId = hoy.Id, ServicioIds = [servicio] }, b.UserId, false);
        }
        using var clienteA = crearCliente(); using var clienteB = crearCliente();
        await ingresar(clienteA, "chatA@prueba.invalid"); await ingresar(clienteB, "chatB@prueba.invalid");
        async Task<string> Token(HttpClient client)
        {
            var html = await client.GetStringAsync("/");
            return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        var tokens = new Dictionary<HttpClient, string>();
        foreach (var client in new[] { clienteA, clienteB, veterinario, administrador }) tokens[client] = await Token(client);
        async Task<HttpResponseMessage> Enviar(HttpClient client, ChatbotAccionSolicitud datos, bool token = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/acciones") { Content = JsonContent.Create(datos) };
            if (token && tokens.TryGetValue(client, out var value)) request.Headers.Add("RequestVerificationToken", value);
            return await client.SendAsync(request);
        }
        async Task<ChatbotAccionRespuesta> Ejecutar(HttpClient client, ChatbotAccionSolicitud datos)
        {
            using var response = await Enviar(client, datos);
            Exigir(response.IsSuccessStatusCode, datos.Accion + ": " + await response.Content.ReadAsStringAsync());
            Exigir(response.Headers.CacheControl?.NoStore == true, "datos cacheables");
            return (await response.Content.ReadFromJsonAsync<ChatbotAccionRespuesta>())!;
        }
        async Task Rechaza(HttpClient client, ChatbotAccionSolicitud datos, HttpStatusCode codigo, bool token = true)
        {
            using var response = await Enviar(client, datos, token);
            Exigir(response.StatusCode == codigo, "no rechazó " + datos.Accion + " con " + codigo);
        }
        var inicio = await Ejecutar(clienteA, new());
        Exigir(inicio.Acciones.Count == 4 && !inicio.Acciones.Any(x => x.Solicitud.Accion.StartsWith("tutores")), "menú Tutor");
        var inicioVet = await Ejecutar(veterinario, new());
        Exigir(inicioVet.Acciones.Any(x => x.Solicitud.Accion == "tutores-faltas"), "menú Veterinario");
        await Rechaza(anonimo, new(), HttpStatusCode.Unauthorized);
        await Rechaza(administrador, new() { Accion = "perros-reserva" }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new(), HttpStatusCode.BadRequest, false);
        await Rechaza(clienteA, new() { Accion = "faltas", TutorId = tutorB }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "vetos", TutorId = tutorA }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "tutores-reserva" }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "patologias", PerroId = perroB }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "atenciones", PerroId = perroB }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "reserva-cupos", PerroId = perroB }, HttpStatusCode.Forbidden);
        await Rechaza(veterinario, new() { Accion = "reserva-cupos", PerroId = perroA, TutorId = tutorB }, HttpStatusCode.Forbidden);
        await Rechaza(clienteA, new() { Accion = "cupos", Pagina = 0 }, HttpStatusCode.BadRequest);
        await Rechaza(clienteA, new() { Accion = "preparar", ServicioIds = [servicio, servicio] }, HttpStatusCode.BadRequest);
        var propios = await Ejecutar(clienteA, new() { Accion = "perros-reserva" });
        Exigir(propios.Acciones.Any(x => x.Etiqueta.Contains("ChatLuna")) && propios.Acciones.All(x => !x.Etiqueta.Contains("ChatPrivado")), "filtración de perros");
        var grilla = await Ejecutar(clienteA, new() { Accion = "turnos" });
        Exigir(grilla.Tablas.Single().Filas.Count == 2 && grilla.Tablas.Single().Filas.All(x => x[1] == "ChatLuna"), "grilla de turnos ajenos");
        Exigir(grilla.Tablas.Single().Filas.Any(x => x[3] == "Completado"), "estado real");
        var clinica = await Ejecutar(clienteA, new() { Accion = "patologias", PerroId = perroA });
        Exigir(clinica.Tablas.Single().Filas.Single()[2] == "Observación clínica privada", "patologías no registradas");
        var atenciones = await Ejecutar(clienteA, new() { Accion = "atenciones", PerroId = perroA });
        var fila = atenciones.Tablas.Single().Filas.Single();
        Exigir(fila[3].Contains("Tratamiento chatbot: Motivo clínico chatbot") && fila[4] == "Atención registrada por el veterinario", "tratamientos u observaciones incompletos");
        var dias = await Ejecutar(clienteA, new() { Accion = "cupos" });
        Exigir(dias.Tablas.Single().Filas.Count <= 10 && dias.Tablas.Single().Filas.All(x => int.Parse(x[1]) > 0), "cupos no disponibles");
        Exigir(dias.Acciones.Any(x => x.Solicitud.Accion == "cupos" && x.Solicitud.Pagina == 2), "falta paginación");
        var segunda = await Ejecutar(clienteA, dias.Acciones.First(x => x.Solicitud.Pagina == 2).Solicitud);
        Exigir(segunda.Acciones.Any(x => x.Etiqueta == "Página anterior"), "no retorna página");
        var tutores = await Ejecutar(veterinario, new() { Accion = "tutores-reserva", Busqueda = "ChatA" });
        Exigir(tutores.BuscarTutor && tutores.Acciones.Any(x => x.Solicitud.TutorId == tutorA), "búsqueda tutor");
        var faltas = await Ejecutar(veterinario, new() { Accion = "faltas", TutorId = tutorA });
        Exigir(faltas.Tablas.Single().Filas.Count == 1, "cancelación real no consultada");
        ChatbotAccionSolicitud Preparar(int perro, int tutor, int cupo) => new() { Accion = "preparar", PerroId = perro, TutorId = tutor, CupoId = cupo, ServicioIds = [servicio] };
        async Task<ChatbotAccionSolicitud> Proponer(HttpClient client, int perro, int tutor, int cupo)
        {
            var respuesta = await Ejecutar(client, Preparar(perro, tutor, cupo));
            return respuesta.Acciones.Single(x => x.Solicitud.Accion == "confirmar").Solicitud;
        }
        var confirmar = await Proponer(clienteA, perroA, tutorA, cupo1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Exigir((await db.CuposDiarios.SingleAsync(x => x.Id == cupo1)).NumeroCuposDisponibles == 1
                && !await db.Turnos.AnyAsync(x => x.CupoDiarioId == cupo1), "preparar realizó una reserva");
        }
        await Rechaza(clienteB, confirmar, HttpStatusCode.BadRequest);
        await Rechaza(clienteA, new() { Accion = "confirmar", Confirmacion = "token adulterado" }, HttpStatusCode.BadRequest);
        var reservado = await Ejecutar(clienteA, confirmar);
        Exigir(reservado.Mensaje.Contains("solicitado correctamente"), "no solicitó");
        await Rechaza(clienteA, confirmar, HttpStatusCode.BadRequest);
        var descartado = await Proponer(clienteA, perroA2, tutorA, cupo2);
        await Ejecutar(clienteA, new() { Accion = "descartar" });
        await Rechaza(clienteA, descartado, HttpStatusCode.BadRequest);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var turno = await db.Turnos.SingleAsync(x => x.CupoDiarioId == cupo1);
            Exigir(turno.PerroId == perroA && turno.EstadoId == EstadosTurno.Solicitado, "reserva incorrecta");
            Exigir((await db.CuposDiarios.SingleAsync(x => x.Id == cupo1)).NumeroCuposDisponibles == 0, "consumo incorrecto");
            await scope.ServiceProvider.GetRequiredService<VetoService>().AplicarAsync(tutorA, usuarioVet, "Veto de prueba chatbot");
        }
        await Rechaza(clienteA, Preparar(perroA2, tutorA, cupo2), HttpStatusCode.BadRequest);
        var vetos = await Ejecutar(veterinario, new() { Accion = "vetos", TutorId = tutorA });
        Exigir(vetos.Tablas.Single().Filas.Single()[4] == "Vigente", "no mostró veto vigente");
        var presencial = await Proponer(veterinario, perroA2, tutorA, cupo2);
        await Ejecutar(veterinario, presencial);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var turno = await db.Turnos.SingleAsync(x => x.CupoDiarioId == cupo2);
            Exigir(await db.AuditoriaAcciones.AnyAsync(x => x.EntidadId == turno.Id && x.Accion == AuditoriaAccion.ReservaPresencial && x.UsuarioId == usuarioVet), "reserva presencial sin auditoría");
            var veto = await db.Vetos.SingleAsync(x => x.TutorId == tutorA);
            await scope.ServiceProvider.GetRequiredService<VetoService>().LevantarAsync(tutorA, veto.Id, usuarioVet, "Fin de prueba chatbot");
        }
        vetos = await Ejecutar(veterinario, new() { Accion = "vetos", TutorId = tutorA });
        Exigir(vetos.Tablas.Single().Filas.Single()[4] == "Finalizado", "no mostró veto finalizado");
        // Dos usuarios confirman por la última plaza: solo uno puede reservar.
        var competirA = await Proponer(clienteA, perroA, tutorA, cupo3);
        var competirVet = await Proponer(veterinario, perroB, tutorB, cupo3);
        var competencias = await Task.WhenAll(Enviar(clienteA, competirA), Enviar(veterinario, competirVet));
        Exigir(competencias.Count(x => x.IsSuccessStatusCode) == 1 && competencias.Count(x => x.StatusCode == HttpStatusCode.BadRequest) == 1, "sobreventa de la última plaza");
        foreach (var respuesta in competencias) respuesta.Dispose();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Exigir(await db.Turnos.CountAsync(x => x.CupoDiarioId == cupo3) == 1
                && (await db.CuposDiarios.SingleAsync(x => x.Id == cupo3)).NumeroCuposDisponibles == 0, "concurrencia no atómica");
        }
        // La propuesta no permite reservar luego de perder disponibilidad.
        await Rechaza(clienteA, Preparar(perroA2, tutorA, cupo1), HttpStatusCode.BadRequest);
        Console.WriteLine("Chatbot operativo: grillas privadas, clínica, cancelaciones, vetos, tutor identificado, confirmación de un solo uso, descarte, reservas normales/presenciales, CSRF, roles y última plaza concurrente verificados.");
    }
}
