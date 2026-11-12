using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services.Chatbot;

// Consultas de solo lectura exclusivas del rol activo Administrador.
public sealed class ChatbotAdministradorService(ApplicationDbContext db, AuditoriaService auditoria)
{
    private const int Tamano = 10;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private static string Fecha(DateTime valor) => valor.ToString("dd/MM/yyyy", Cultura);
    private static DateTime Utc(DateTime fecha) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified),
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));
    private sealed record Totales(int Solicitudes, int Completados, int CanceladosTutor, int CanceladosVeterinario);

    public async Task<ChatbotAccionRespuesta> EjecutarAsync(ChatbotAccionSolicitud solicitud, ClaimsPrincipal usuario)
    {
        if (!usuario.IsInRole(RolSesion.Administrador)) throw new UnauthorizedAccessException();
        var acciones = new List<ChatbotAccion>();
        var tablas = new List<ChatbotTabla>();
        ChatbotAccionSolicitud Nueva(string accion, int pagina = 1, string? periodo = null) => new()
            { Accion = accion, Pagina = pagina, Periodo = periodo ?? solicitud.Periodo };
        void Boton(string texto, ChatbotAccionSolicitud siguiente) => acciones.Add(new(texto, siguiente));
        if (solicitud.Accion is "inicio" or "descartar")
        {
            Boton("Servicios más solicitados", Nueva("admin-servicios"));
            Boton("Turnos completados y cancelados", Nueva("admin-estados"));
            Boton("Comparar este mes con el anterior", Nueva("admin-comparar"));
            Boton("Últimas acciones de auditoría", Nueva("admin-auditoria"));
            return new("Consultas de administración. Elegí qué información querés ver.", tablas, acciones);
        }
        if (!solicitud.Accion.StartsWith("admin-", StringComparison.Ordinal)) throw new UnauthorizedAccessException();
        if (solicitud.Accion == "admin-auditoria")
        {
            var pagina = await auditoria.ConsultarAsync(solicitud.Pagina, null);
            var estados = await db.Estados.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Descripcion);
            string Estado(int? id) => id.HasValue ? estados.GetValueOrDefault(id.Value, $"Estado #{id}") : "—";
            tablas.Add(new("Últimas acciones de auditoría", ["Evento", "Fecha (Argentina)", "Usuario", "Rol activo", "Acción", "Entidad", "Registro", "Tutor", "Estado anterior", "Estado nuevo"],
                pagina.Eventos.Select(x => new[] { x.Id.ToString(), Reloj.EnArgentina(x.FechaUtc).ToString("dd/MM/yyyy HH:mm:ss", Cultura),
                    x.Usuario, x.RolActivo, x.Accion, x.Entidad, x.EntidadId.ToString(), x.Tutor + $" (#{x.TutorId})", Estado(x.EstadoAnteriorId), Estado(x.EstadoNuevoId) }).ToList()));
            if (solicitud.Pagina > 1) Boton("Página anterior", Nueva("admin-auditoria", solicitud.Pagina - 1));
            if (pagina.HaySiguiente) Boton("Página siguiente", Nueva("admin-auditoria", solicitud.Pagina + 1));
            Boton("Volver al inicio del asistente", Nueva("inicio"));
            return new("Eventos registrados, del más reciente al más antiguo. Incluye registros con baja lógica.", tablas, acciones);
        }
        if (solicitud.Accion is not ("admin-servicios" or "admin-estados" or "admin-comparar"))
            throw new InvalidOperationException("La consulta administrativa indicada no está disponible.");

        var hoy = Reloj.Hoy;
        var actual = new DateTime(hoy.Year, hoy.Month, 1);
        var anterior = actual.AddMonths(-1);
        var desde = solicitud.Periodo == "mes-anterior" ? anterior : actual;
        var hastaExclusivo = solicitud.Periodo == "mes-anterior" ? actual : hoy.AddDays(1);
        var inicio = Utc(desde);
        var fin = Utc(hastaExclusivo);
        var etiqueta = $"{Fecha(desde)} al {Fecha(hastaExclusivo.AddDays(-1))}";
        var mensaje = "";
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var eventos = db.AuditoriaAcciones.AsNoTracking().IgnoreQueryFilters();
        var cobertura = await eventos.MinAsync(x => (DateTime?)x.FechaUtc);
        var eventosTurno = eventos.Where(x => x.Entidad == nameof(Turno));
        var creaciones = eventosTurno.Where(x => x.Accion == AuditoriaAccion.TurnoSolicitado || x.Accion == AuditoriaAccion.ReservaPresencial)
            .GroupBy(x => x.EntidadId).Select(x => new { TurnoId = x.Key, Fecha = x.Min(a => a.FechaUtc) });
        async Task<int> ContarEstado(int estado, DateTime principio, DateTime final) =>
            await eventosTurno.Where(x => x.Accion == AuditoriaAccion.EstadoTurnoCambiado && x.EstadoNuevoId == estado)
                .GroupBy(x => x.EntidadId).Select(x => x.Min(e => e.FechaUtc)).CountAsync(x => x >= principio && x < final);
        async Task<Totales> Contar(DateTime principio, DateTime final) => new(
            await creaciones.CountAsync(x => x.Fecha >= principio && x.Fecha < final),
            await ContarEstado(EstadosTurno.Completado, principio, final),
            await ContarEstado(EstadosTurno.CanceladoTutor, principio, final),
            await ContarEstado(EstadosTurno.CanceladoVeterinario, principio, final));
        if (solicitud.Accion == "admin-servicios")
        {
            var ranking = await (from relacion in db.ServiciosPorTurno.AsNoTracking().IgnoreQueryFilters()
                join creacion in creaciones on relacion.TurnoId equals creacion.TurnoId
                where creacion.Fecha >= inicio && creacion.Fecha < fin
                group relacion by new { relacion.ServicioId, relacion.Servicio.Descripcion } into grupo
                orderby grupo.Count() descending, grupo.Key.Descripcion, grupo.Key.ServicioId
                select new { grupo.Key.ServicioId, grupo.Key.Descripcion, Cantidad = grupo.Count() })
                .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1).ToListAsync();
            tablas.Add(new("Servicios más solicitados · " + etiqueta, ["Servicio", "Solicitudes"], ranking.Take(Tamano)
                .Select(x => new[] { x.Descripcion, x.Cantidad.ToString() }).ToList()));
            mensaje = "Cantidad de turnos que solicitaron cada servicio, según la fecha de registro de la solicitud; incluye reservas presenciales y turnos cancelados posteriormente.";
            if (solicitud.Pagina > 1) Boton("Página anterior", Nueva(solicitud.Accion, solicitud.Pagina - 1));
            if (ranking.Count > Tamano) Boton("Página siguiente", Nueva(solicitud.Accion, solicitud.Pagina + 1));
        }
        else if (solicitud.Accion == "admin-estados")
        {
            var totales = await Contar(inicio, fin);
            tablas.Add(new("Actividad · " + etiqueta, ["Indicador", "Cantidad"],
                [new[] { "Solicitudes registradas", totales.Solicitudes.ToString() }, new[] { "Turnos completados", totales.Completados.ToString() },
                 new[] { "Cancelados por el tutor", totales.CanceladosTutor.ToString() }, new[] { "Cancelados por el veterinario", totales.CanceladosVeterinario.ToString() },
                 new[] { "Total de turnos cancelados", (totales.CanceladosTutor + totales.CanceladosVeterinario).ToString() }]));
            mensaje = "Cada indicador usa la fecha real de su evento de auditoría, no el día agendado. Los rechazados no se cuentan como cancelados. Los indicadores pueden corresponder a turnos solicitados en otro período.";
        }
        else
        {
            var previo = await Contar(Utc(anterior), Utc(actual));
            var presente = await Contar(Utc(actual), Utc(hoy.AddDays(1)));
            string[] Fila(string nombre, int antes, int ahora) => [nombre, antes.ToString(), ahora.ToString(), (ahora - antes).ToString("+0;-0;0", Cultura)];
            tablas.Add(new("Actividad mensual", ["Indicador", $"Mes anterior ({Fecha(anterior)}–{Fecha(actual.AddDays(-1))})", $"Mes actual ({Fecha(actual)}–{Fecha(hoy)})", "Diferencia"],
                [Fila("Solicitudes", previo.Solicitudes, presente.Solicitudes), Fila("Completados", previo.Completados, presente.Completados),
                 Fila("Cancelados por el tutor", previo.CanceladosTutor, presente.CanceladosTutor), Fila("Cancelados por el veterinario", previo.CanceladosVeterinario, presente.CanceladosVeterinario),
                 Fila("Total cancelados", previo.CanceladosTutor + previo.CanceladosVeterinario, presente.CanceladosTutor + presente.CanceladosVeterinario)]));
            mensaje = "Comparación por fechas reales de los eventos. El mes anterior se muestra completo y el actual hasta hoy (en curso); la diferencia no es una proyección ni una comparación de períodos de igual duración.";
        }
        await transaccion.CommitAsync();
        mensaje += cobertura.HasValue ? $" Cobertura de auditoría desde {Fecha(Reloj.EnArgentina(cobertura.Value))}; los períodos anteriores pueden tener información incompleta."
            : " Todavía no hay eventos de auditoría; no se puede establecer cobertura histórica.";
        if (solicitud.Accion != "admin-comparar")
        {
            Boton("Consultar este mes", Nueva(solicitud.Accion, periodo: "mes-actual"));
            Boton("Consultar mes anterior", Nueva(solicitud.Accion, periodo: "mes-anterior"));
        }
        Boton("Volver al inicio del asistente", Nueva("inicio"));
        return new(mensaje, tablas, acciones);
    }
}
