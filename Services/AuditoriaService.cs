using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services;

public class AuditoriaService(ApplicationDbContext db)
{
    // El servicio que realiza la operación guarda el evento dentro de su transacción.
    public void RegistrarTurno(Turno turno, int tutorId, string usuarioId, bool esVeterinario,
        string accion, int? estadoAnteriorId = null)
    {
        db.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            FechaUtc = DateTime.UtcNow,
            UsuarioId = usuarioId,
            TutorId = tutorId,
            RolActivo = esVeterinario ? "Veterinario" : "Tutor",
            Accion = accion,
            Entidad = nameof(Turno),
            EntidadId = turno.Id,
            EstadoAnteriorId = estadoAnteriorId,
            EstadoNuevoId = turno.EstadoId
        });
    }

    public void RegistrarVeto(Veto veto, string usuarioId, bool levantado, bool automatico = false)
    {
        db.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            FechaUtc = levantado ? veto.FechaFin!.Value : veto.FechaInicio,
            UsuarioId = usuarioId,
            TutorId = veto.TutorId,
            // El veto automático lo dispara la cancelación del Tutor, no una acción veterinaria.
            RolActivo = automatico ? "Tutor" : "Veterinario",
            Accion = levantado ? AuditoriaAccion.VetoLevantado
                : automatico ? AuditoriaAccion.VetoAutomaticoAplicado : AuditoriaAccion.VetoAplicado,
            Entidad = nameof(Veto),
            EntidadId = veto.Id
        });
    }

    public async Task<AuditoriaPaginaDto> ConsultarAsync(int pagina, int? tutorId)
    {
        const int tamano = 25;
        // El historial incluye las cuentas y los tutores que recibieron una baja lógica.
        var consulta = db.AuditoriaAcciones.IgnoreQueryFilters().AsNoTracking();
        if (tutorId.HasValue) consulta = consulta.Where(x => x.TutorId == tutorId.Value);
        var eventos = await consulta.OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamano).Take(tamano + 1)
            .Select(x => new AuditoriaDto(x.Id, x.FechaUtc, x.UsuarioId, x.Usuario.Nombre + " " + x.Usuario.Apellido,
                x.RolActivo, x.Accion, x.Entidad, x.EntidadId, x.TutorId, x.Tutor.Usuario.Nombre + " " + x.Tutor.Usuario.Apellido,
                x.EstadoAnteriorId, x.EstadoNuevoId))
            .ToListAsync();
        var haySiguiente = eventos.Count > tamano;
        if (haySiguiente) eventos.RemoveAt(tamano);
        return new AuditoriaPaginaDto(eventos, pagina, haySiguiente);
    }

    public async Task<(int Total, int Filtrados, string[][] Filas)> ConsultarTablaAsync(
        int inicio, int cantidad, int? tutorId, string? buscar, bool ascendente)
    {
        var consulta = db.AuditoriaAcciones.IgnoreQueryFilters().AsNoTracking();
        if (tutorId.HasValue) consulta = consulta.Where(x => x.TutorId == tutorId.Value);
        var total = await consulta.CountAsync();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            foreach (var palabra in buscar.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var patron = "%" + palabra.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                var acciones = EtiquetasAccion.Where(x => x.Value.Contains(palabra, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Key).ToArray();
                consulta = consulta.Where(x => EF.Functions.ILike(
                    x.Usuario.Nombre + " " + x.Usuario.Apellido + " " + x.Tutor.Usuario.Nombre + " " +
                    x.Tutor.Usuario.Apellido + " " + x.RolActivo + " " + x.Entidad + " " + x.Accion, patron)
                    || acciones.Contains(x.Accion));
            }
        }

        var filtrados = string.IsNullOrWhiteSpace(buscar) ? total : await consulta.CountAsync();
        var orden = ascendente ? consulta.OrderBy(x => x.FechaUtc).ThenBy(x => x.Id)
            : consulta.OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id);
        var eventos = await orden.Skip(inicio).Take(cantidad).Select(x => new AuditoriaDto(
            x.Id, x.FechaUtc, x.UsuarioId, x.Usuario.Nombre + " " + x.Usuario.Apellido, x.RolActivo,
            x.Accion, x.Entidad, x.EntidadId, x.TutorId, x.Tutor.Usuario.Nombre + " " + x.Tutor.Usuario.Apellido,
            x.EstadoAnteriorId, x.EstadoNuevoId)).ToListAsync();

        return (total, filtrados, eventos.Select(x => new[]
        {
            Reloj.EnArgentina(x.FechaUtc).ToString("dd/MM/yyyy HH:mm:ss"),
            x.Usuario, x.RolActivo, $"{x.Tutor} (#{x.TutorId})", EtiquetaAccion(x.Accion),
            $"{x.Entidad} #{x.EntidadId}",
            (x.EstadoAnteriorId.HasValue ? EtiquetaEstado(x.EstadoAnteriorId) + " → " : "") + EtiquetaEstado(x.EstadoNuevoId)
        }).ToArray());
    }

    private static readonly IReadOnlyDictionary<string, string> EtiquetasAccion = new Dictionary<string, string>
    {
        [AuditoriaAccion.TurnoSolicitado] = "Solicitud de turno",
        [AuditoriaAccion.ReservaPresencial] = "Reserva presencial",
        [AuditoriaAccion.TurnoEditado] = "Edición de turno",
        [AuditoriaAccion.EstadoTurnoCambiado] = "Cambio de estado",
        [AuditoriaAccion.VetoAutomaticoAplicado] = "Veto automático por cancelaciones",
        [AuditoriaAccion.VetoAplicado] = "Aplicación de veto",
        [AuditoriaAccion.VetoLevantado] = "Levantamiento de veto"
    };

    public static string EtiquetaAccion(string accion) => EtiquetasAccion.GetValueOrDefault(accion, accion);

    public static string EtiquetaEstado(int? id) => id switch
    {
        EstadosTurno.Solicitado => "Solicitado",
        EstadosTurno.Confirmado => "Confirmado",
        EstadosTurno.Rechazado => "Rechazado",
        EstadosTurno.Completado => "Completado",
        EstadosTurno.CanceladoTutor => "Cancelado por tutor",
        EstadosTurno.CanceladoVeterinario => "Cancelado por veterinario",
        _ => "—"
    };
}
