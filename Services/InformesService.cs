using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services;

public class InformesService(ApplicationDbContext db)
{
    public static string Titulo(TipoInforme tipo) => tipo switch
    {
        TipoInforme.FrecuenciaSolicitudes => "Frecuencia de solicitudes",
        TipoInforme.ServiciosCompletos => "Atenciones con servicios completos",
        TipoInforme.ServiciosIncompletos => "Atenciones con servicios incompletos",
        _ => throw new InvalidOperationException("Seleccioná un informe válido.")
    };

    public static string Criterio(TipoInforme tipo) => tipo switch
    {
        TipoInforme.FrecuenciaSolicitudes => "Solicitudes normales y reservas presenciales según su fecha de registro, cualquiera sea su estado posterior.",
        TipoInforme.ServiciosCompletos => "Atenciones finalizadas en el período con al menos un servicio y todos sus servicios realizados.",
        TipoInforme.ServiciosIncompletos => "Atenciones finalizadas en el período con al menos un servicio sin realizar. Las patologías, nombres y observaciones son los registrados actualmente.",
        _ => throw new InvalidOperationException("Seleccioná un informe válido.")
    };

    public async Task<InformeDto> ConsultarAsync(TipoInforme tipo, PeriodoInformeDto periodo, CancellationToken ct = default)
    {
        _ = Titulo(tipo);
        var validaciones = new List<ValidationResult>();
        if (!Validator.TryValidateObject(periodo, new ValidationContext(periodo), validaciones, true))
            throw new InvalidOperationException(validaciones[0].ErrorMessage);
        var desde = periodo.Desde!.Value;
        var hasta = periodo.Hasta!.Value;
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var inicio = TimeZoneInfo.ConvertTimeToUtc(desde.ToDateTime(TimeOnly.MinValue), zona);
        var fin = TimeZoneInfo.ConvertTimeToUtc(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), zona);

        // Un mismo snapshot para total, grillas y fecha de cobertura.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var eventos = db.AuditoriaAcciones.AsNoTracking().IgnoreQueryFilters();
        var auditoriaDesde = await eventos.MinAsync(x => (DateTime?)x.FechaUtc, ct);
        var solicitudes = new List<SolicitudesPorDiaDto>();
        var atenciones = new List<AtencionInformeDto>();
        var patologias = new List<PatologiaInformeDto>();
        int total;
        if (tipo == TipoInforme.FrecuenciaSolicitudes)
        {
            // Cada turno se cuenta una vez, por su primer evento de creación.
            var fechas = await eventos.Where(x => x.Entidad == nameof(Turno)
                    && (x.Accion == AuditoriaAccion.TurnoSolicitado || x.Accion == AuditoriaAccion.ReservaPresencial))
                .GroupBy(x => x.EntidadId).Select(x => x.Min(a => a.FechaUtc))
                .Where(x => x >= inicio && x < fin).ToListAsync(ct);
            solicitudes = fechas.GroupBy(x => DateOnly.FromDateTime(Reloj.EnArgentina(x)))
                .OrderBy(x => x.Key).Select(x => new SolicitudesPorDiaDto(x.Key, x.Count())).ToList();
            total = fechas.Count;
        }
        else
        {
            var finalizaciones = eventos.Where(x => x.Entidad == nameof(Turno)
                    && x.Accion == AuditoriaAccion.EstadoTurnoCambiado && x.EstadoNuevoId == EstadosTurno.Completado)
                .GroupBy(x => x.EntidadId).Select(x => new { TurnoId = x.Key, FechaUtc = x.Min(a => a.FechaUtc) });
            var servicios = db.ServiciosPorTurno.AsNoTracking().IgnoreQueryFilters();
            // Las bajas lógicas no borran una atención del informe histórico.
            var consulta = from turno in db.Turnos.AsNoTracking().IgnoreQueryFilters()
                           join evento in finalizaciones on turno.Id equals evento.TurnoId
                           where turno.EstadoId == EstadosTurno.Completado && evento.FechaUtc >= inicio && evento.FechaUtc < fin
                           select new { Turno = turno, evento.FechaUtc };
            consulta = tipo == TipoInforme.ServiciosCompletos
                ? consulta.Where(x => servicios.Any(s => s.TurnoId == x.Turno.Id)
                    && !servicios.Any(s => s.TurnoId == x.Turno.Id && !s.Completado))
                : consulta.Where(x => servicios.Any(s => s.TurnoId == x.Turno.Id && !s.Completado));
            atenciones = await consulta.OrderBy(x => x.FechaUtc).ThenBy(x => x.Turno.Id)
                .Select(x => new AtencionInformeDto(x.Turno.Id, x.Turno.PerroId, x.Turno.Perro.Nombre,
                    x.Turno.Perro.Tutor.Usuario.Nombre + " " + x.Turno.Perro.Tutor.Usuario.Apellido, x.FechaUtc,
                    servicios.Count(s => s.TurnoId == x.Turno.Id),
                    servicios.Count(s => s.TurnoId == x.Turno.Id && s.Completado), x.Turno.Observaciones)).ToListAsync(ct);
            total = atenciones.Count;
            if (tipo == TipoInforme.ServiciosIncompletos)
            {
                patologias = await (from atencion in consulta
                    join relacion in db.PerrosPatologias.AsNoTracking().IgnoreQueryFilters() on atencion.Turno.PerroId equals relacion.PerroId
                    group relacion by new { relacion.PatologiaId, relacion.Patologia.Descripcion } into grupo
                    orderby grupo.Count() descending, grupo.Key.Descripcion, grupo.Key.PatologiaId
                    select new PatologiaInformeDto(grupo.Key.Descripcion, grupo.Count())).ToListAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
        return new InformeDto(tipo, desde, hasta, auditoriaDesde, total, solicitudes, atenciones, patologias);
    }
}
