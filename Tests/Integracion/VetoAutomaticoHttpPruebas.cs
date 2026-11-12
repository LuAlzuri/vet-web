using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static partial class VetoAutomaticoPruebas
{
    public static async Task EjecutarHttpAsync(ServiceProvider provider, HttpClient tutor, HttpClient veterinario,
        HttpClient administrador, int perroId, int servicioId, string tokenTutor, string tokenVeterinario)
    {
        int tutorId;
        await using (var scope = provider.CreateAsyncScope())
            tutorId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Perros
                .Where(x => x.Id == perroId).Select(x => x.TutorId).SingleAsync();
        async Task<HttpResponseMessage> Post(HttpClient cliente, string ruta, object datos, string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(datos) };
            request.Headers.Add("RequestVerificationToken", token);
            return await cliente.SendAsync(request);
        }
        async Task<TurnoDto> Solicitud()
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fecha = (await db.CuposDiarios.MaxAsync(x => x.FechaCupo)).AddDays(1);
            var cupo = new CupoDiario { FechaCupo = fecha, NumeroCuposDisponibles = 2 };
            db.CuposDiarios.Add(cupo); await db.SaveChangesAsync();
            return new TurnoDto { PerroId = perroId, CupoDiarioId = cupo.Id, ServicioIds = new() { servicioId } };
        }
        var urlVetos = $"/api/tutores/{tutorId}/vetos";
        for (var numero = 1; numero <= 3; numero++)
        {
            using var creada = await Post(tutor, "/api/turnos", await Solicitud(), tokenTutor);
            Exigir(creada.StatusCode == HttpStatusCode.Created, "la API no creó el turno de prueba.");
            var turno = (await creada.Content.ReadFromJsonAsync<TurnoRespuestaDto>())!;
            if (numero == 1)
            {
                using var cancelada = await tutor.PostAsync($"/Turnos/CancelarPersonal?id={turno.Id}",
                    new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = tokenTutor }));
                Exigir(cancelada.IsSuccessStatusCode && (await cancelada.Content.ReadAsStringAsync()).Contains("true"), "falló la cancelación web.");
            }
            else
            {
                using var cancelada = await Post(tutor, $"/api/turnos/{turno.Id}/cancelar", new { }, tokenTutor);
                Exigir(cancelada.StatusCode == HttpStatusCode.NoContent, "falló la cancelación API.");
            }
            var historial = (await veterinario.GetFromJsonAsync<List<VetoDto>>(urlVetos))!;
            Exigir(historial.Count(x => x.Vigente) == (numero == 3 ? 1 : 0), "web y API no comparten el umbral.");
        }
        var veto = (await veterinario.GetFromJsonAsync<List<VetoDto>>(urlVetos))!.Single(x => x.Vigente);
        Exigir(veto.VeterinarioId is null && veto.Motivo == Veto.MotivoAutomatico, "la API no distingue el veto automático.");
        var ficha = WebUtility.HtmlDecode(await veterinario.GetStringAsync($"/Vetos/Listado?tutorId={tutorId}"));
        Exigir(ficha.Contains("Automático") && ficha.Contains("Levantar veto") && ficha.Contains("Reserva presencial"),
            "la ficha no muestra el origen o las acciones del veto automático.");
        var solicitud = await Solicitud();
        using var bloqueada = await Post(tutor, "/api/turnos", solicitud, tokenTutor);
        Exigir(bloqueada.StatusCode == HttpStatusCode.BadRequest
            && (await bloqueada.Content.ReadAsStringAsync()).Contains("veto vigente"), "la API no explica el bloqueo.");
        using var bloqueadaWeb = await tutor.PostAsync("/Turnos/GuardarPersonal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenTutor, ["PerroId"] = perroId.ToString(),
            ["CupoDiarioId"] = solicitud.CupoDiarioId.ToString(), ["ServicioIds"] = servicioId.ToString()
        }));
        Exigir(bloqueadaWeb.StatusCode == HttpStatusCode.BadRequest
            && (await bloqueadaWeb.Content.ReadAsStringAsync()).Contains("veto vigente"), "la web no explica el bloqueo.");
        using var presencial = await Post(veterinario, "/api/turnos/presencial", solicitud, tokenVeterinario);
        Exigir(presencial.StatusCode == HttpStatusCode.Created, "falló la excepción presencial con veto automático.");
        var reservado = (await presencial.Content.ReadFromJsonAsync<TurnoRespuestaDto>())!;
        Exigir(reservado.VeterinarioId is not null, "la reserva presencial no tiene veterinario.");
        using var levantado = await veterinario.PostAsync($"/Vetos/Guardar?tutorId={tutorId}&vetoId={veto.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = tokenVeterinario, ["Motivo"] = "Levantamiento automático desde web" }));
        Exigir(levantado.IsSuccessStatusCode, "la web no pudo levantar el veto automático.");
        using var nueva = await Post(tutor, "/api/turnos", await Solicitud(), tokenTutor);
        Exigir(nueva.StatusCode == HttpStatusCode.Created, "levantar desde web no habilitó la API.");
        var nuevo = (await nueva.Content.ReadFromJsonAsync<TurnoRespuestaDto>())!;
        using var cancelar = await Post(tutor, $"/api/turnos/{nuevo.Id}/cancelar", new { }, tokenTutor);
        Exigir(cancelar.StatusCode == HttpStatusCode.NoContent
            && !(await veterinario.GetFromJsonAsync<List<VetoDto>>(urlVetos))!.Any(x => x.Vigente), "no se reinició el conteo por HTTP.");
        var auditoria = (await administrador.GetFromJsonAsync<AuditoriaPaginaDto>($"/api/auditoria?tutorId={tutorId}"))!;
        Exigir(auditoria.Eventos.Any(x => x.Accion == AuditoriaAccion.VetoAutomaticoAplicado && x.RolActivo == "Tutor"
            && x.EntidadId == veto.Id), "falta el veto automático en la auditoría API.");
        Console.WriteLine("Veto automático HTTP: cancelación web/API, bloqueo con mensaje, ficha, reserva presencial, levantamiento web y auditoría API verificados.");
    }
}
